using Apolon.Core.DataAccess;
using Apolon.Core.Migrations;

namespace Apolon.CLI.Services;

internal static class MigrationExecutor
{
    public static async Task<int> ExecuteMigrationsAsync(
        string connectionString,
        string migrationsPath,
        string? targetMigration = null)
    {
        Console.WriteLine($"Discovering migrations in: {Path.GetFullPath(migrationsPath)}");
        var migrationTypes = await TypeDiscovery.DiscoverMigrationTypes(migrationsPath);

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

        await using var connection = new DbConnectionNpgsql(connectionString);
        await connection.OpenConnectionAsync();

        var runner = await MigrationRunner.CreateAsync(connection);

        var applied = await runner.GetAppliedMigrationsAsync();
        Console.WriteLine($"\nAlready applied: {applied.Count} migration(s)");

        var toRun = MigrationRunner.DetermineMigrationsToRun(migrationTypes, applied, targetMigration);
        if (toRun.Count == 0)
        {
            Console.WriteLine("\n✓ Database is up to date. No migrations to apply.");
            return 0;
        }

        Console.WriteLine($"\nApplying {toRun.Count} migration(s)...");
        // Optionally print each migration being applied
        foreach (var (_, _, _, fullName) in toRun)
        {
            Console.WriteLine($"  - {fullName}");
        }

        var executed = await runner.ApplyMigrationsAsync(migrationTypes, targetMigration);
        Console.WriteLine($"\n✓ Successfully applied {executed} migration(s)");
        return executed;
    }

    public static async Task<int> RollbackMigrationsAsync(
        string connectionString,
        string migrationsPath,
        string targetMigration)
    {
        Console.WriteLine($"Discovering migrations in: {Path.GetFullPath(migrationsPath)}");
        var migrationTypes = await TypeDiscovery.DiscoverMigrationTypes(migrationsPath);

        if (migrationTypes.Length == 0)
        {
            Console.WriteLine("No migrations found.");
            return 0;
        }

        await using var connection = new DbConnectionNpgsql(connectionString);
        await connection.OpenConnectionAsync();

        var runner = await MigrationRunner.CreateAsync(connection);
        var applied = await runner.GetAppliedMigrationsAsync();

        if (applied.Count == 0)
        {
            Console.WriteLine("No migrations have been applied yet.");
            return 0;
        }

        // Preview rollbacks
        var toRollback = MigrationRunner.GetMigrationsToRollback(migrationTypes, applied, targetMigration);
        if (toRollback.Count == 0)
        {
            Console.WriteLine($"\n✓ Database is already at migration '{targetMigration}'");
            return 0;
        }

        Console.WriteLine($"\nRolling back {toRollback.Count} migration(s) to reach '{targetMigration}':");
        foreach (var full in toRollback)
        {
            Console.WriteLine($"  - {full}");
        }

        Console.WriteLine("\n⚠ Warning: Rollback operations may result in data loss!");
        Console.Write("Continue? (y/N): ");
        var confirm = Console.ReadLine()?.Trim().ToLower();

        if (confirm != "y" && confirm != "yes")
        {
            Console.WriteLine("Rollback cancelled.");
            return 0;
        }

        var rolledBack = await runner.RollbackToAsync(migrationTypes, targetMigration);
        Console.WriteLine($"\n✓ Successfully rolled back {rolledBack} migration(s)");
        return rolledBack;
    }
}