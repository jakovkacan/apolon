using Apolon.Core.Attributes;
using Apolon.Core.DataAccess;
using Apolon.Core.Migrations.Models;
using Apolon.Core.Migrations.Utils;
using Apolon.Core.Sql;

namespace Apolon.Core.Migrations;

public class MigrationRunner
{
    private readonly IDbConnection _connection;
    private readonly EntityExecutor _executor;

    private MigrationRunner(IDbConnection connection)
    {
        _connection = connection;
        _executor = new EntityExecutor(connection);
    }

    public static async Task<MigrationRunner> CreateAsync(
        IDbConnection connection)
    {
        var runner = new MigrationRunner(connection);
        await runner.EnsureMigrationHistoryTable();
        return runner;
    }

    internal async Task RunPendingMigrations(params MigrationTypeWrapper[] migrationTypes)
    {
        foreach (var migrationType in migrationTypes)
        {
            // if (await IsMigrationApplied(migrationType.Name)) continue;

            var migration =
                (Migration)(Activator.CreateInstance(migrationType.Type) ?? throw new InvalidOperationException());
            var builder = new MigrationBuilder();
            migration.Up(builder);

            // Convert operations to SQL and execute
            var sqlBatch = MigrationUtils.ConvertOperationsToSql(builder.Operations);
            await ExecuteSqlAsync(sqlBatch);

            await RecordMigration(migrationType.Name);
        }
    }

    public async Task RollbackLastMigration()
    {
        var lastMigration = await GetLastAppliedMigration();
        if (lastMigration != null)
        {
            var migrationType = Type.GetType($"Apolon.Migrations.{lastMigration}");
            if (migrationType != null)
            {
                var builder = new MigrationBuilder();
                var migration =
                    (Migration)(Activator.CreateInstance(migrationType) ?? throw new InvalidOperationException());
                migration.Down(builder);
                await RemoveMigration(lastMigration);
            }
        }
    }

    public async Task<IReadOnlyList<string>> SyncSchemaAsync(
        CancellationToken ct = default,
        params Type[] entityTypes)
    {
        // 1) snapshots
        var dbSnapshot = await SnapshotReader.ReadAsync(_connection, ct);
        var modelSnapshot = ModelSnapshotBuilder.BuildFromModel(entityTypes);

        // 2) diff -> ops
        var ops = SchemaDiffer.Diff(modelSnapshot, dbSnapshot);

        // 3) ops -> sql batch
        var sqlBatch = new List<string>();

        // Create schema/table ops first
        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.CreateSchema))
            sqlBatch.Add(MigrationBuilderSql.BuildCreateSchema(op.Schema));

        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.CreateTable))
            sqlBatch.Add(MigrationBuilderSql.BuildCreateTableFromName(op.Schema, op.Table));

        // Column changes
        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.AddColumn))
            sqlBatch.Add(MigrationBuilderSql.BuildAddColumn(
                op.Schema,
                op.Table,
                op.Column!,
                op.GetSqlType()!,
                op.IsNullable!.Value,
                op.DefaultSql,
                op.IsPrimaryKey ?? false,
                op.IsIdentity ?? false,
                op.IdentityGeneration));

        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.AlterColumnType))
            sqlBatch.Add(MigrationBuilderSql.BuildAlterColumnType(op.Schema, op.Table, op.Column!, op.GetSqlType()!));

        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.AlterNullability))
            sqlBatch.Add(
                MigrationBuilderSql.BuildAlterNullability(op.Schema, op.Table, op.Column!, op.IsNullable!.Value));

        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.SetDefault))
            sqlBatch.Add(MigrationBuilderSql.BuildSetDefault(op.Schema, op.Table, op.Column!, op.DefaultSql!));

        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.DropDefault))
            sqlBatch.Add(MigrationBuilderSql.BuildDropDefault(op.Schema, op.Table, op.Column!));

        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.DropConstraint))
            sqlBatch.Add(MigrationBuilderSql.BuildDropConstraint(op.Schema, op.Table, op.ConstraintName!));

        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.DropColumn))
            sqlBatch.Add(MigrationBuilderSql.BuildDropColumn(op.Schema, op.Table, op.Column!));

        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.DropTable))
            sqlBatch.Add(MigrationBuilderSql.BuildDropTableFromName(op.Schema, op.Table));

        // Constraints last (safer)
        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.AddUnique))
            sqlBatch.Add(MigrationBuilderSql.BuildAddUnique(op.Schema, op.Table, op.Column!));

        // FK add: if you want model-driven OnDelete behavior, extend MigrationOp to carry it.
        foreach (var op in ops.Where(o => o.Type is MigrationOperationType.AddForeignKey))
        {
            // Default ON DELETE NO ACTION unless you pass richer info in the op.
            sqlBatch.Add(MigrationBuilderSql.BuildAddForeignKey(
                schema: op.Schema,
                table: op.Table,
                column: op.Column!,
                constraintName: op.ConstraintName ?? $"{op.Table}_{op.Column}_fkey",
                refSchema: op.RefSchema ?? "public",
                refTable: op.RefTable ?? throw new InvalidOperationException("Missing ref table"),
                refColumn: op.RefColumn ?? "id",
                onDelete: MigrationUtils.ParseOnDeleteRule(op.OnDeleteRule)
            ));
        }

        // 4) apply in one transaction
        if (sqlBatch.Count == 0)
            return sqlBatch;

        await _connection.BeginTransactionAsync(ct);
        try
        {
            foreach (var sql in sqlBatch)
                await _connection.ExecuteNonQueryAsync(_connection.CreateCommand(sql));

            await _connection.CommitTransactionAsync(ct);
            return sqlBatch;
        }
        catch
        {
            await _connection.RollbackTransactionAsync(ct);
            throw;
        }
    }

    private async Task ExecuteSqlAsync(List<string> sqlBatch, CancellationToken ct = default)
    {
        if (sqlBatch.Count == 0)
            return;

        await _connection.BeginTransactionAsync(ct);
        try
        {
            foreach (var sql in sqlBatch)
                await _connection.ExecuteNonQueryAsync(_connection.CreateCommand(sql));

            await _connection.CommitTransactionAsync(ct);
        }
        catch
        {
            await _connection.RollbackTransactionAsync(ct);
            throw;
        }
    }

    private async Task EnsureMigrationHistoryTable()
    {
        List<string> sql =
        [
            MigrationBuilderSql.BuildCreateSchema("apolon"),
            MigrationBuilderSql.BuildCreateTable(typeof(MigrationHistoryTable))
        ];

        await ExecuteSqlAsync(sql);
    }

    private async Task<bool> IsMigrationApplied(string migrationName)
    {
        var qb = new QueryBuilder<MigrationHistoryTable>()
            .Where(m => m.MigrationName == migrationName);

        var command = _connection.CreateCommand(qb.Build());
        foreach (var param in qb.GetParameters())
        {
            _connection.AddParameter(command, param.Name, param.Value);
        }

        return await _connection.ExecuteScalarAsync(command) != null;
    }

    private async Task RecordMigration(string migrationName, string? productVersion = null)
    {
        await _executor.InsertAsync(new MigrationHistoryTable
            { MigrationName = migrationName, ProductVersion = productVersion });
    }

    private async Task RemoveMigration(string migrationName)
    {
        await _executor.DeleteAsync(new MigrationHistoryTable { MigrationName = migrationName });
    }

    private async Task<string?> GetLastAppliedMigration()
    {
        var qb = new QueryBuilder<MigrationHistoryTable>()
            .OrderByDescending(m => m.AppliedAt)
            .Take(1);

        return (await _connection.ExecuteScalarAsync(_connection.CreateCommand(qb.Build())))?.ToString();
    }
}