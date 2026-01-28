using System.Reflection;
using Apolon.Core.DataAccess;
using Apolon.Core.Mapping;
using Apolon.Core.Mapping.Models;
using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;
using Apolon.Core.Migrations.Utils;

namespace Apolon.CLI.Services;

internal class MigrationExecutor
{
    public async Task<int> ExecuteMigrationsAsync(
        string connectionString,
        string migrationsPath,
        string? targetMigration = null)
    {
        // Discover migration types (auto-build if needed)
        Console.WriteLine($"Discovering migrations in: {Path.GetFullPath(migrationsPath)}");
        var migrationTypes = TypeDiscovery.DiscoverMigrationTypes(migrationsPath);

        if (migrationTypes.Length == 0)
        {
            Console.WriteLine("No migrations found.");
            return 0;
        }

        Console.WriteLine($"Found {migrationTypes.Length} migration(s):");
        foreach (var (_, timestamp, name) in migrationTypes)
        {
            Console.WriteLine($"  - {timestamp}_{name}");
        }

        // Connect to database
        await using var connection = new DbConnectionNpgsql(connectionString);
        await connection.OpenConnectionAsync();

        var runner = await MigrationRunner.CreateAsync(connection);

        // Get applied migrations
        var appliedMigrations = await GetAppliedMigrationsAsync(connection);
        Console.WriteLine($"\nAlready applied: {appliedMigrations.Count} migration(s)");

        // Determine which migrations to run
        var migrationsToRun = DetermineMigrationsToRun(
            migrationTypes,
            appliedMigrations,
            targetMigration);

        if (migrationsToRun.Count == 0)
        {
            Console.WriteLine("\n✓ Database is up to date. No migrations to apply.");
            return 0;
        }

        Console.WriteLine($"\nApplying {migrationsToRun.Count} migration(s)...");

        // Execute migrations
        var executed = 0;
        foreach (var (type, timestamp, name) in migrationsToRun)
        {
            Console.Write($"  Applying {timestamp}_{name}... ");
            try
            {
                var migration = new MigrationTypeWrapper
                {
                    Type = type,
                    Name = name,
                    Timestamp = timestamp
                };
                await runner.RunMigrations(migration);
                Console.WriteLine("✓");
                executed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine("✗");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    Error: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        Console.WriteLine($"\n✓ Successfully applied {executed} migration(s)");
        return executed;
    }

    public static async Task<int> RollbackMigrationsAsync(
        string connectionString,
        string migrationsPath,
        string targetMigration)
    {
        // Discover migration types
        Console.WriteLine($"Discovering migrations in: {Path.GetFullPath(migrationsPath)}");
        var migrationTypes = TypeDiscovery.DiscoverMigrationTypes(migrationsPath);

        if (migrationTypes.Length == 0)
        {
            Console.WriteLine("No migrations found.");
            return 0;
        }

        // Connect to database
        await using var connection = new DbConnectionNpgsql(connectionString);
        await connection.OpenConnectionAsync();

        // Get applied migrations
        var appliedMigrations = await GetAppliedMigrationsAsync(connection);

        if (appliedMigrations.Count == 0)
        {
            Console.WriteLine("No migrations have been applied yet.");
            return 0;
        }

        // Find target migration index
        var targetFullName = migrationTypes
            .FirstOrDefault(m => m.Name.Equals(targetMigration, StringComparison.OrdinalIgnoreCase)
                                 || $"{m.Timestamp}_{m.Name}".Equals(targetMigration,
                                     StringComparison.OrdinalIgnoreCase));

        if (targetFullName.Type == null)
        {
            throw new InvalidOperationException($"Target migration '{targetMigration}' not found.");
        }

        // Determine migrations to rollback (in reverse order)
        var migrationsToRollback = migrationTypes
            .Where(m => string.Compare($"{m.Timestamp}_{m.Name}",
                $"{targetFullName.Timestamp}_{targetFullName.Name}",
                StringComparison.Ordinal) > 0)
            .Where(m => appliedMigrations.Contains(m.Name))
            .OrderByDescending(m => m.Timestamp)
            .ToList();

        if (migrationsToRollback.Count == 0)
        {
            Console.WriteLine($"\n✓ Database is already at migration '{targetMigration}'");
            return 0;
        }

        Console.WriteLine($"\nRolling back {migrationsToRollback.Count} migration(s) to reach '{targetMigration}':");
        foreach (var (_, timestamp, name) in migrationsToRollback)
        {
            Console.WriteLine($"  - {timestamp}_{name}");
        }

        Console.WriteLine("\n⚠ Warning: Rollback operations may result in data loss!");
        Console.Write("Continue? (y/N): ");
        var confirm = Console.ReadLine()?.Trim().ToLower();

        if (confirm != "y" && confirm != "yes")
        {
            Console.WriteLine("Rollback cancelled.");
            return 0;
        }

        // Execute rollbacks
        var rolledBack = 0;
        foreach (var (type, timestamp, name) in migrationsToRollback)
        {
            Console.Write($"  Rolling back {timestamp}_{name}... ");
            try
            {
                var migration = (Migration)Activator.CreateInstance(type)!;
                var builder = new MigrationBuilder();
                migration.Down(builder);

                // Execute Down operations
                var sqlBatch = MigrationUtils.ConvertOperationsToSql(builder.Operations);
                await ExecuteSqlAsync(connection, sqlBatch);

                // Remove from history
                await RemoveMigrationFromHistoryAsync(connection, $"{timestamp}_{name}");

                Console.WriteLine("✓");
                rolledBack++;
            }
            catch (Exception ex)
            {
                Console.WriteLine("✗");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    Error: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        Console.WriteLine($"\n✓ Successfully rolled back {rolledBack} migration(s)");
        return rolledBack;
    }

    private static async Task<List<string>> GetAppliedMigrationsAsync(IDbConnection connection)
    {
        var migrationTable = EntityMapper.GetMetadata(typeof(MigrationHistoryTable));

        var sql =
            $"SELECT migration_name FROM {migrationTable.Schema}.{migrationTable.TableName} ORDER BY applied_at";
        var command = connection.CreateCommand(sql);

        var results = new List<string>();
        await using var reader = await connection.ExecuteReaderAsync(command);

        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    private static List<(Type Type, string Timestamp, string Name)> DetermineMigrationsToRun(
        (Type Type, string Timestamp, string Name)[] allMigrations,
        List<string> appliedMigrations,
        string? targetMigration)
    {
        var toRun = new List<(Type Type, string Timestamp, string Name)>();

        foreach (var migration in allMigrations)
        {
            var fullName = migration.Name;

            // Skip already applied
            if (appliedMigrations.Contains(fullName))
                continue;

            toRun.Add(migration);

            // Stop if we reached target
            if (targetMigration != null &&
                (migration.Name.Equals(targetMigration, StringComparison.OrdinalIgnoreCase) ||
                 fullName.Equals(targetMigration, StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }
        }

        return toRun;
    }

    private static async Task ExecuteSqlAsync(IDbConnection connection, List<string> sqlBatch)
    {
        if (sqlBatch.Count == 0)
            return;

        await connection.BeginTransactionAsync();
        try
        {
            foreach (var sql in sqlBatch)
            {
                await connection.ExecuteNonQueryAsync(connection.CreateCommand(sql));
            }

            await connection.CommitTransactionAsync();
        }
        catch
        {
            await connection.RollbackTransactionAsync();
            throw;
        }
    }

    private static async Task RemoveMigrationFromHistoryAsync(IDbConnection connection, string migrationName)
    {
        const string sql = "DELETE FROM apolon.__apolon_migrations WHERE migration_name = @migrationName";
        var command = connection.CreateCommand(sql);
        connection.AddParameter(command, "@migrationName", migrationName);
        await connection.ExecuteNonQueryAsync(command);
    }
}