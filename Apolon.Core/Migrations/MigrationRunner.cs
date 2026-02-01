using Apolon.Core.DataAccess;
using Apolon.Core.Migrations.Models;
using Apolon.Core.Migrations.Utils;
using Apolon.Core.Sql;

namespace Apolon.Core.Migrations;

public class MigrationRunner
{
    private readonly DatabaseExecutor _executor;

    private MigrationRunner(IDbConnection connection)
    {
        _executor = new DatabaseExecutor(connection);
    }

    public static async Task<MigrationRunner> CreateAsync(IDbConnection connection)
    {
        var runner = new MigrationRunner(connection);
        await runner.EnsureMigrationHistoryTable();
        return runner;
    }

    // Public: returns currently applied migration names (stored in history)
    public async Task<List<string>> GetAppliedMigrationsAsync(CancellationToken ct = default)
    {
        var qb = new QueryBuilder<MigrationHistoryTable>().OrderBy(m => m.AppliedAt);
        var results = await _executor.QueryAsync(qb);

        return results.Select(m => m.MigrationName).ToList();
    }

    // Public: determine which migrations from discovery need to be applied (fullName = "{timestamp}_{name}")
    public static List<(Type Type, string Timestamp, string Name, string FullName)> DetermineMigrationsToRun(
        (Type Type, string Timestamp, string Name)[] allMigrations,
        List<string> appliedMigrations,
        string? targetMigration)
    {
        var toRun = new List<(Type, string, string, string)>();

        foreach (var migration in allMigrations)
        {
            var fullName = $"{migration.Timestamp}_{migration.Name}";

            // Skip already applied
            if (appliedMigrations.Contains(fullName))
                continue;

            toRun.Add((migration.Type, migration.Timestamp, migration.Name, fullName));

            // Stop if we reached target
            if (targetMigration != null &&
                (migration.Name.Equals(targetMigration, StringComparison.OrdinalIgnoreCase) ||
                 fullName.Equals(targetMigration, StringComparison.OrdinalIgnoreCase)))
                break;
        }

        return toRun;
    }

    // Apply migrations (returns number applied)
    public async Task<int> ApplyMigrationsAsync(
        (Type Type, string Timestamp, string Name)[] discoveredMigrations,
        string? targetMigration = null,
        CancellationToken ct = default)
    {
        var applied = await GetAppliedMigrationsAsync(ct);
        var toRun = DetermineMigrationsToRun(discoveredMigrations, applied, targetMigration);

        if (toRun.Count == 0)
            return 0;

        var executed = 0;
        foreach (var (type, timestamp, name, fullName) in toRun)
        {
            var wrapper = new MigrationTypeWrapper
            {
                Type = type,
                Name = fullName,
                Timestamp = timestamp
            };

            // Use existing internal runner to construct & execute Up
            await RunMigrations(wrapper);
            executed++;
        }

        return executed;
    }

    // Public: preview which migrations would be rolled back to a target (returns full names in descending order)
    public static List<string> GetMigrationsToRollback(
        (Type Type, string Timestamp, string Name)[] allMigrations,
        List<string> appliedMigrations,
        string targetMigration)
    {
        var target = allMigrations
            .FirstOrDefault(m => m.Name.Equals(targetMigration, StringComparison.OrdinalIgnoreCase)
                                 || $"{m.Timestamp}_{m.Name}".Equals(targetMigration,
                                     StringComparison.OrdinalIgnoreCase));

        var targetFull = target.Type == null ? null : $"{target.Timestamp}_{target.Name}";

        if (targetFull == null)
            throw new InvalidOperationException($"Target migration '{targetMigration}' not found.");

        var list = allMigrations
            .Select(m => new { m.Type, m.Timestamp, m.Name, Full = $"{m.Timestamp}_{m.Name}" })
            .Where(x => string.Compare(x.Full, targetFull, StringComparison.Ordinal) > 0)
            .Where(x => appliedMigrations.Contains(x.Full))
            .OrderByDescending(x => x.Timestamp)
            .Select(x => x.Full)
            .ToList();

        return list;
    }

    // Public: rollback migrations to reach target. Returns number rolled back.
    public async Task<int> RollbackToAsync(
        (Type Type, string Timestamp, string Name)[] discoveredMigrations,
        string targetMigration,
        CancellationToken ct = default)
    {
        var applied = await GetAppliedMigrationsAsync(ct);
        var toRollbackFullNames = GetMigrationsToRollback(discoveredMigrations, applied, targetMigration);

        if (toRollbackFullNames.Count == 0)
            return 0;

        var map = discoveredMigrations.ToDictionary(
            m => $"{m.Timestamp}_{m.Name}",
            m => m.Type);

        var rolledBack = 0;
        foreach (var fullName in toRollbackFullNames)
        {
            if (!map.TryGetValue(fullName, out var type))
                continue;

            var migration = (Migration)(Activator.CreateInstance(type) ?? throw new InvalidOperationException());
            var builder = new MigrationBuilder();
            migration.Down(builder);

            var sqlBatch = MigrationUtils.ConvertOperationsToSql(builder.Operations);
            await _executor.ExecuteSqlAsync(sqlBatch, ct);

            await RemoveMigration(fullName);
            rolledBack++;
        }

        return rolledBack;
    }

    // internal helper: executes provided migration wrappers (assumes wrapper.Name is the stored full name)
    private async Task RunMigrations(params MigrationTypeWrapper[] migrationTypes)
    {
        foreach (var migrationType in migrationTypes)
        {
            var migration =
                (Migration)(Activator.CreateInstance(migrationType.Type) ?? throw new InvalidOperationException());
            var builder = new MigrationBuilder();
            migration.Up(builder);

            var sqlBatch = MigrationUtils.ConvertOperationsToSql(builder.Operations);
            await _executor.ExecuteSqlAsync(sqlBatch);

            await RecordMigration(migrationType.Name);
        }
    }

    // public async Task<IReadOnlyList<string>> SyncSchemaAsync(
    //     CancellationToken ct = default,
    //     params Type[] entityTypes)
    // {
    //     var dbSnapshot = await SnapshotReader.ReadAsync(_connection, ct);
    //     var modelSnapshot = SnapshotBuilder.BuildFromModel(entityTypes);
    //
    //     var ops = SchemaDiffer.Diff(modelSnapshot, dbSnapshot);
    //     var sqlBatch = MigrationUtils.ConvertOperationsToSql(ops);
    //
    //     if (sqlBatch.Count == 0)
    //         return sqlBatch;
    //
    //     await _connection.BeginTransactionAsync(ct);
    //     try
    //     {
    //         foreach (var sql in sqlBatch)
    //             await _connection.ExecuteNonQueryAsync(_connection.CreateCommand(sql));
    //
    //         await _connection.CommitTransactionAsync(ct);
    //         return sqlBatch;
    //     }
    //     catch
    //     {
    //         await _connection.RollbackTransactionAsync(ct);
    //         throw;
    //     }
    // }

    private async Task EnsureMigrationHistoryTable()
    {
        var sql = new List<string>
        {
            MigrationBuilderSql.BuildCreateSchema("apolon"),
            MigrationBuilderSql.BuildCreateTable(typeof(MigrationHistoryTable))
        };

        await _executor.ExecuteSqlAsync(sql);
    }

    private async Task<bool> IsMigrationApplied(string migrationName)
    {
        var qb = new QueryBuilder<MigrationHistoryTable>()
            .Where(m => m.MigrationName == migrationName);

        var result = await _executor.QueryAsync(qb);

        return result.Count != 0;
    }

    private async Task RecordMigration(string migrationName, string? productVersion = null)
    {
        await _executor.InsertAsync(new MigrationHistoryTable
            { MigrationName = migrationName, ProductVersion = productVersion });
    }

    private async Task RemoveMigration(string migrationName)
    {
        var qb = new QueryBuilder<MigrationHistoryTable>().Where(m => m.MigrationName == migrationName);
        var result = await _executor.QueryAsync(qb);

        if (result.Count == 0)
            return;

        await _executor.DeleteAsync(result[0]);
    }
}