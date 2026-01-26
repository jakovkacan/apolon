using Apolon.Core.DataAccess;
using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;

namespace Apolon.CLI.Services;

internal class MigrationGenerator
{
    public async Task<string> GenerateMigrationAsync(
        string migrationName,
        string modelsPath,
        string migrationsPath,
        string connectionString,
        string namespaceName)
    {
        Console.WriteLine($"Discovering entity types in: {Path.GetFullPath(modelsPath)}");
        var entityTypes = TypeDiscovery.DiscoverEntityTypes(modelsPath);
        Console.WriteLine($"Found {entityTypes.Length} entity types: {string.Join(", ", entityTypes.Select(t => t.Name))}");

        Console.WriteLine("Building model snapshot...");
        var modelSnapshot = ModelSnapshotBuilder.BuildFromModel(entityTypes);

        Console.WriteLine("Fetching database snapshot...");
        var dbSnapshot = await FetchDatabaseSnapshotAsync(connectionString);

        Console.WriteLine("Comparing snapshots...");
        var operations = SchemaDiffer.Diff(modelSnapshot, dbSnapshot);
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

        Console.WriteLine($"\nGenerating migration file: {migrationName}");
        var migrationCode = MigrationCodeGenerator.GenerateMigrationCode(migrationName, operations, namespaceName);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{timestamp}_{migrationName}.cs";
        var fullPath = Path.Combine(migrationsPath, fileName);

        Directory.CreateDirectory(migrationsPath);
        await File.WriteAllTextAsync(fullPath, migrationCode);

        Console.WriteLine($"Migration generated successfully at: {fullPath}");
        return fullPath;
    }

    private async Task<SchemaSnapshot> FetchDatabaseSnapshotAsync(string connectionString)
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
}
