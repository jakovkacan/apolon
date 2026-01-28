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
        var entityTypes = TypeDiscovery.DiscoverEntityTypes(modelsPath, hasTableAttribute: true);
        Console.WriteLine(
            $"Found {entityTypes.Length} entity types: {string.Join(", ", entityTypes.Select(t => t.Name))}");

        Console.WriteLine("Building model snapshot...");
        var modelSnapshot = ModelSnapshotBuilder.BuildFromModel(entityTypes);

        Console.WriteLine("Fetching database snapshot...");
        var dbSnapshot = await FetchDatabaseSnapshotAsync(connectionString);

        Console.WriteLine("Fetching commited migrations...");
        var migrationTypes = TypeDiscovery.DiscoverMigrationTypes(migrationsPath);
        var committedMigrations = migrationTypes
            .Select(mt => mt.Type)
            .ToList();

        Console.WriteLine("Fetching commited operations...");
        var committedOperations = MigrationUtils.ExtractOperationsFromMigrationTypes(committedMigrations);

        Console.WriteLine("Comparing snapshots...");
        var operations = SchemaDiffer.Diff(modelSnapshot, dbSnapshot, committedOperations);
        Console.WriteLine($"Found {operations.Count} migration operations");

        if (operations.Count == 0)
        {
            Console.WriteLine("No changes detected. Database is already up to date with models.");
            return string.Empty;
        }

        // Display operations
        // Console.WriteLine("\nOperations to be generated:");
        // foreach (var op in operations)
        // {
        //     Console.WriteLine($"  - {op.Type}: {op.Schema}.{op.Table}" +
        //                       (op.Column != null ? $".{op.Column}" : ""));
        // }

        Console.WriteLine($"\nGenerating migration file: {sanitizedName}");
        var migrationCode =
            FluentMigrationCodeGenerator.GenerateMigrationCode(sanitizedName, operations, namespaceName, committedOperations);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{timestamp}_{sanitizedName}.cs";
        var fullPath = Path.Combine(migrationsPath, fileName);

        Directory.CreateDirectory(migrationsPath);
        await File.WriteAllTextAsync(fullPath, migrationCode);

        Console.WriteLine($"Migration generated successfully at: {fullPath}");
        return fullPath;
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