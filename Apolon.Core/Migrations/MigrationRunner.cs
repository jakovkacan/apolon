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
        EnsureMigrationHistoryTable();
    }

    public void RunPendingMigrations(params Type[] migrationTypes)
    {
        foreach (var migrationType in migrationTypes)
        {
            if (!IsMigrationApplied(migrationType.Name))
            {
                var migration = (Migration)Activator.CreateInstance(migrationType);
                migration.Up();
                RecordMigration(migrationType.Name);
            }
        }
    }

    public void RollbackLastMigration()
    {
        var lastMigration = GetLastAppliedMigration();
        if (lastMigration != null)
        {
            var migrationName = lastMigration;
            var migrationType = Type.GetType($"ApolonORM.Migrations.{migrationName}");
            if (migrationType != null)
            {
                var migration = (Migration)Activator.CreateInstance(migrationType);
                migration.Down();
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
        foreach (var op in ops.Where(o => o.Kind is MigrationOpKind.CreateSchema))
            sqlBatch.Add(MigrationBuilder.BuildCreateSchema(op.Schema));

        foreach (var op in ops.Where(o => o.Kind is MigrationOpKind.CreateTable))
            sqlBatch.Add(MigrationBuilder.BuildCreateTableFromName(op.Schema, op.Table));

        // Column changes
        foreach (var op in ops.Where(o => o.Kind is MigrationOpKind.AddColumn))
            sqlBatch.Add(MigrationBuilder.BuildAddColumn(op.Schema, op.Table, op.Column!, op.SqlType!, op.IsNullable!.Value, op.DefaultSql));

        foreach (var op in ops.Where(o => o.Kind is MigrationOpKind.AlterColumnType))
            sqlBatch.Add(MigrationBuilder.BuildAlterColumnType(op.Schema, op.Table, op.Column!, op.SqlType!));

        foreach (var op in ops.Where(o => o.Kind is MigrationOpKind.AlterNullability))
            sqlBatch.Add(MigrationBuilder.BuildAlterNullability(op.Schema, op.Table, op.Column!, op.IsNullable!.Value));

        foreach (var op in ops.Where(o => o.Kind is MigrationOpKind.SetDefault))
            sqlBatch.Add(MigrationBuilder.BuildSetDefault(op.Schema, op.Table, op.Column!, op.DefaultSql!));

        foreach (var op in ops.Where(o => o.Kind is MigrationOpKind.DropDefault))
            sqlBatch.Add(MigrationBuilder.BuildDropDefault(op.Schema, op.Table, op.Column!));

        // Constraints last (safer)
        foreach (var op in ops.Where(o => o.Kind is MigrationOpKind.AddUnique))
            sqlBatch.Add(MigrationBuilder.BuildAddUnique(op.Schema, op.Table, op.Column!));

        // FK add: if you want model-driven OnDelete behavior, extend MigrationOp to carry it.
        foreach (var op in ops.Where(o => o.Kind is MigrationOpKind.AddForeignKey))
        {
            // Default ON DELETE NO ACTION unless you pass richer info in the op.
            sqlBatch.Add(MigrationBuilder.BuildAddForeignKey(
                schema: op.Schema,
                table: op.Table,
                column: op.Column!,
                constraintName: op.ConstraintName ?? $"fk_{op.Table}_{op.Column}",
                refSchema: op.RefSchema ?? "public",
                refTable: op.RefTable ?? throw new InvalidOperationException("Missing ref table"),
                refColumn: op.RefColumn ?? "id",
                onDelete: OnDeleteBehavior.NoAction
            ));
        }

        // 4) apply in one transaction
        if (sqlBatch.Count == 0)
            return sqlBatch;

        _connection.BeginTransaction();
        try
        {
            foreach (var sql in sqlBatch)
                _connection.ExecuteNonQuery(_connection.CreateCommand(sql));

            _connection.CommitTransaction();
            return sqlBatch;
        }
        catch
        {
            _connection.RollbackTransaction();
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
}