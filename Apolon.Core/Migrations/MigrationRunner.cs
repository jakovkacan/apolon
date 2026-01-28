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

    internal async Task RunMigrations(params MigrationTypeWrapper[] migrationTypes)
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

        // 3) ops -> sql batch (operations are automatically sorted by dependency order)
        var sqlBatch = MigrationUtils.ConvertOperationsToSql(ops);

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