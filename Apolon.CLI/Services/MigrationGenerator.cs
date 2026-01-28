using Apolon.Core.DataAccess;
using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;
using Apolon.Core.Migrations.Utils;

namespace Apolon.CLI.Services;

internal static class MigrationGenerator
{
    public static async Task<string> GenerateMigrationAsync(
        string migrationName,
        string modelsPath,
        string migrationsPath,
        string connectionString,
        string namespaceName)
    {
        var sanitizedName = SanitizeMigrationName(migrationName);

        Console.WriteLine($"Discovering entity types in: {Path.GetFullPath(modelsPath)}");
        var entityTypes = await TypeDiscovery.DiscoverEntityTypes(modelsPath, hasTableAttribute: true);
        Console.WriteLine(
            $"Found {entityTypes.Length} entity types: {string.Join(", ", entityTypes.Select(t => t.Name))}");

        Console.WriteLine("Building model snapshot...");
        var modelSnapshot = SnapshotBuilder.BuildFromModel(entityTypes);

        Console.WriteLine("Fetching database snapshot...");
        var dbSnapshot = await FetchDatabaseSnapshotAsync(connectionString);

        Console.WriteLine("Fetching applied migrations...");
        var appliedMigrations = await FetchAppliedMigrationsAsync(connectionString);
        appliedMigrations = appliedMigrations?.Select(m => m[(m.IndexOf('_') + 1)..]).ToList();
        
        Console.WriteLine($"Found {appliedMigrations?.Count ?? 0} applied migrations: {string.Join(", ", appliedMigrations ?? [])}");

        Console.WriteLine("Fetching commited migrations...");
        var migrationTypes = await TypeDiscovery.DiscoverMigrationTypes(migrationsPath, rebuildProject: false);
        var committedMigrations = migrationTypes
            .Where(mt => !appliedMigrations?.Contains($"{mt.Name}") ?? true)
            .Select(mt => mt.Type)
            .ToList();
        
        Console.WriteLine($"Found {committedMigrations.Count} commited but not applied migrations: {string.Join(", ", committedMigrations.Select(m => m.Name))}");
        
        var committedOperations = MigrationUtils.ExtractOperationsFromMigrationTypes(committedMigrations);
        var commitedSnapshot = SnapshotBuilder.ApplyMigrations(dbSnapshot, committedOperations);
        
        Console.WriteLine("Comparing snapshots...");
        var operations = SchemaDiffer.Diff(modelSnapshot, commitedSnapshot);
        Console.WriteLine($"Found {operations.Count} migration operations");

        if (operations.Count == 0)
        {
            Console.WriteLine("No changes detected. Database is already up to date with models.");
            return string.Empty;
        }

        // Display operations
        Console.WriteLine("\nOperations to be generated:");
        foreach (var op in operations)
        {
            Console.WriteLine($"  - {op.Type}: {op.Schema}.{op.Table}" +
                              (op.Column != null ? $".{op.Column}" : ""));
        }

        Console.WriteLine($"\nGenerating migration file: {sanitizedName}");
        var migrationCode =
            MigrationCodeGenerator.GenerateMigrationCode(sanitizedName, operations, namespaceName, committedOperations);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{timestamp}_{sanitizedName}.cs";
        var fullPath = Path.Combine(migrationsPath, fileName);

        Directory.CreateDirectory(migrationsPath);
        await File.WriteAllTextAsync(fullPath, migrationCode);

        Console.WriteLine($"Migration generated successfully at: {fullPath}");
        return fullPath;
    }

    private static async Task<List<string>?> FetchAppliedMigrationsAsync(string connectionString)
    {
        await using var connection = new DbConnectionNpgsql(connectionString);
        await connection.OpenConnectionAsync();

        var runner = await MigrationRunner.CreateAsync(connection);
        return await runner.GetAppliedMigrationsAsync();
    }

    private static async Task<SchemaSnapshot> FetchDatabaseSnapshotAsync(string connectionString)
    {
        await using var connection = new DbConnectionNpgsql(connectionString);
        await connection.OpenConnectionAsync();

        try
        {
            var snapshot = await SnapshotReader.ReadAsync(connection);
            return snapshot;
        }
        finally
        {
            connection.CloseConnection();
        }
    }

    private static string SanitizeMigrationName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Migration name cannot be empty.", nameof(name));

        // Remove invalid characters and replace spaces with underscores
        var sanitized = new string(name
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray());

        // If it starts with a digit, prefix with underscore
        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;

        // Ensure PascalCase (capitalize first letter after sanitization)
        if (sanitized.Length > 0 && char.IsLower(sanitized[0]))
            sanitized = char.ToUpper(sanitized[0]) + sanitized.Substring(1);

        return sanitized;
    }
}