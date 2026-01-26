using Apolon.Core.Attributes;
using Apolon.Core.DataAccess;
using Apolon.Core.Migrations.Models;
using Apolon.Core.Sql;

namespace Apolon.Core.Migrations;

public class MigrationRunner
{
    private readonly IDbConnection _connection;
    private readonly string _migrationsPath;
    private readonly EntityExecutor _executor;

    internal MigrationRunner(IDbConnection connection, string migrationsPath = "./Migrations")
    {
        _connection = connection;
        _migrationsPath = migrationsPath;
        _executor = new EntityExecutor(connection);
        // EnsureMigrationHistoryTable();
    }

    public void RunPendingMigrations(params Type[] migrationTypes)
    {
        foreach (var migrationType in migrationTypes)
        {
            // if (!IsMigrationApplied(migrationType.Name))
            {
                var migration = (Migration)Activator.CreateInstance(migrationType);
                var builder = new MigrationBuilder();
                migration.Up(builder);

                // Convert operations to SQL and execute
                var sqlBatch = ConvertOperationsToSql(builder.Operations);
                ExecuteSqlAsync(sqlBatch).Wait();

                // RecordMigration(migrationType.Name);
            }
        }
    }

    public void RollbackLastMigration()
    {
        var lastMigration = GetLastAppliedMigration();
        if (lastMigration != null)
        {
            var migrationName = lastMigration;
            var migrationType = Type.GetType($"Apolon.Migrations.{migrationName}");
            if (migrationType != null)
            {
                var migration = (Migration)Activator.CreateInstance(migrationType);
                // migration.Down();
                RemoveMigration(migrationName);
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
            sqlBatch.Add(MigrationBuilderSql.BuildAlterNullability(op.Schema, op.Table, op.Column!, op.IsNullable!.Value));

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
                onDelete: ParseOnDeleteRule(op.OnDeleteRule)
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

    internal async Task ExecuteSqlAsync(List<string> sqlBatch, CancellationToken ct = default)
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

    private void EnsureMigrationHistoryTable()
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS public.__EFMigrationsHistory (
                migration_id VARCHAR(150) PRIMARY KEY,
                product_version VARCHAR(32) NOT NULL,
                applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
        ";
        _connection.ExecuteNonQuery(_connection.CreateCommand(sql));
    }

    private bool IsMigrationApplied(string migrationId)
    {
        var qb = new QueryBuilder<MigrationHistory>()
            .Where(m => m.MigrationId == migrationId);

        var command = _connection.CreateCommand(qb.Build());
        foreach (var param in qb.GetParameters())
        {
            _connection.AddParameter(command, param.Name, param.Value);
        }

        return _connection.ExecuteScalar(command) != null;
    }

    private void RecordMigration(string migrationId)
    {
        _executor.Insert(new MigrationHistory { MigrationId = migrationId, ProductVersion = "1.0" });
    }

    private void RemoveMigration(string migrationId)
    {
        _executor.Delete(new MigrationHistory { MigrationId = migrationId });
    }

    private string? GetLastAppliedMigration()
    {
        var qb = new QueryBuilder<MigrationHistory>()
            .OrderByDescending(m => m.AppliedAt)
            .Take(1);

        return _connection.ExecuteScalar(_connection.CreateCommand(qb.Build()))?.ToString();
    }

    private static OnDeleteBehavior ParseOnDeleteRule(string? rule)
    {
        return rule?.ToUpperInvariant() switch
        {
            "CASCADE" => OnDeleteBehavior.Cascade,
            "RESTRICT" => OnDeleteBehavior.Restrict,
            "SET NULL" => OnDeleteBehavior.SetNull,
            "SET DEFAULT" => OnDeleteBehavior.SetDefault,
            "NO ACTION" => OnDeleteBehavior.NoAction,
            "NOACTION" => OnDeleteBehavior.NoAction,
            _ => OnDeleteBehavior.NoAction
        };
    }

    private static List<string> ConvertOperationsToSql(IReadOnlyList<MigrationOperation> operations)
    {
        var sql = new List<string>();
        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case MigrationOperationType.CreateSchema:
                    sql.Add(MigrationBuilderSql.BuildCreateSchema(op.Schema));
                    break;
                case MigrationOperationType.CreateTable:
                    sql.Add(MigrationBuilderSql.BuildCreateTableFromName(op.Schema, op.Table));
                    break;
                case MigrationOperationType.AddColumn:
                    sql.Add(MigrationBuilderSql.BuildAddColumn(
                        op.Schema, op.Table, op.Column!, op.GetSqlType()!,
                        op.IsNullable!.Value, op.DefaultSql,
                        op.IsPrimaryKey ?? false, op.IsIdentity ?? false, op.IdentityGeneration));
                    break;
                case MigrationOperationType.DropTable:
                    sql.Add(MigrationBuilderSql.BuildDropTableFromName(op.Schema, op.Table));
                    break;
                case MigrationOperationType.DropColumn:
                    sql.Add(MigrationBuilderSql.BuildDropColumn(op.Schema, op.Table, op.Column!));
                    break;
                case MigrationOperationType.AlterColumnType:
                    sql.Add(MigrationBuilderSql.BuildAlterColumnType(op.Schema, op.Table, op.Column!,
                        op.GetSqlType()!));
                    break;
                case MigrationOperationType.AlterNullability:
                    sql.Add(MigrationBuilderSql.BuildAlterNullability(op.Schema, op.Table, op.Column!,
                        op.IsNullable!.Value));
                    break;
                case MigrationOperationType.SetDefault:
                    sql.Add(MigrationBuilderSql.BuildSetDefault(op.Schema, op.Table, op.Column!, op.DefaultSql!));
                    break;
                case MigrationOperationType.DropDefault:
                    sql.Add(MigrationBuilderSql.BuildDropDefault(op.Schema, op.Table, op.Column!));
                    break;
                case MigrationOperationType.AddUnique:
                    sql.Add(MigrationBuilderSql.BuildAddUnique(op.Schema, op.Table, op.Column!));
                    break;
                case MigrationOperationType.DropConstraint:
                    sql.Add(MigrationBuilderSql.BuildDropConstraint(op.Schema, op.Table, op.ConstraintName!));
                    break;
                case MigrationOperationType.AddForeignKey:
                    sql.Add(MigrationBuilderSql.BuildAddForeignKey(
                        schema: op.Schema,
                        table: op.Table,
                        column: op.Column!,
                        constraintName: op.ConstraintName ?? $"{op.Table}_{op.Column}_fkey",
                        refSchema: op.RefSchema ?? "public",
                        refTable: op.RefTable ?? throw new InvalidOperationException("Missing ref table"),
                        refColumn: op.RefColumn ?? "id",
                        onDelete: ParseOnDeleteRule(op.OnDeleteRule)
                    ));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported migration operation type: {op.Type}");
            }
        }

        return sql;
    }
}
