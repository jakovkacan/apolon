using System.Text;
using Apolon.Core.DataAccess;
using Npgsql;

namespace Apolon.CLI.Services;

internal static class DbContextGenerator
{
    public static async Task<string> GenerateDbContextAsync(
        string connectionString,
        string modelsPath,
        string? outputPath = null,
        string? namespaceName = null)
    {
        // Validate database connection first
        Console.WriteLine("Validating database connection...");
        await ValidateDatabaseConnectionAsync(connectionString);
        Console.WriteLine("Database connection successful");

        // Extract database name from connection string
        var databaseName = ExtractDatabaseName(connectionString);
        var contextClassName = $"{ToPascalCase(databaseName)}DbContext";

        // Discover entity classes from source files
        var entities = SourceFileParser.DiscoverEntityClassesFromSource(modelsPath);

        Console.WriteLine($"\nFound {entities.Length} entity classes:");
        foreach (var (className, ns) in entities)
        {
            Console.WriteLine($"  - {className} ({ns})");
        }

        // Determine output path (default to models directory)
        var finalOutputPath = outputPath ?? modelsPath;
        if (!Directory.Exists(finalOutputPath))
        {
            Directory.CreateDirectory(finalOutputPath);
        }

        // Determine namespace (infer from directory if not specified)
        var finalNamespace = namespaceName ?? InferNamespace(finalOutputPath);

        // Get the models namespace from the first entity (they should all be in the same namespace)
        var modelsNamespace = entities.Length > 0 ? entities[0].Namespace : InferNamespace(modelsPath);

        // Generate the DbContext code
        var code = GenerateDbContextCode(contextClassName, finalNamespace, modelsNamespace,
            entities.Select(e => e.ClassName).ToArray(), connectionString);

        // Write to file
        var filePath = Path.Combine(finalOutputPath, $"{contextClassName}.cs");
        await File.WriteAllTextAsync(filePath, code);

        return filePath;
    }

    private static async Task ValidateDatabaseConnectionAsync(string connectionString)
    {
        try
        {
            await using var connection = new DbConnectionNpgsql(connectionString);
            await connection.OpenConnectionAsync();
            connection.CloseConnection();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to connect to database. Please check your connection string.\nError: {ex.Message}", ex);
        }
    }

    private static string ExtractDatabaseName(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var database = builder.Database;

            return string.IsNullOrEmpty(database)
                ? throw new InvalidOperationException("Database name not found in connection string")
                : database;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse connection string: {ex.Message}", ex);
        }
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove special characters and split by common separators
        var words = input.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries);

        var sb = new StringBuilder();
        foreach (var word in words)
        {
            if (word.Length <= 0) continue;

            sb.Append(char.ToUpper(word[0]));
            if (word.Length > 1)
            {
                sb.Append(word[1..].ToLower());
            }
        }

        return sb.ToString();
    }

    private static string InferNamespace(string path)
    {
        // Get the directory name as namespace
        var dirInfo = new DirectoryInfo(Path.GetFullPath(path));

        // Try to find a .csproj file to determine project name
        var projectFiles = dirInfo.GetFiles("*.csproj", SearchOption.TopDirectoryOnly);
        return projectFiles.Length > 0
            ? Path.GetFileNameWithoutExtension(projectFiles[0].Name)
            : dirInfo.Name;
    }

    private static string GenerateDbContextCode(
        string className,
        string namespaceName,
        string modelsNamespace,
        string[] entityClassNames,
        string connectionString)
    {
        var sb = new StringBuilder();

        // Using directives
        sb.AppendLine("using Apolon.Core.Context;");
        sb.AppendLine("using Apolon.Core.DbSet;");

        // Add models namespace if different
        if (modelsNamespace != namespaceName)
        {
            sb.AppendLine($"using {modelsNamespace};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        // Class declaration
        sb.AppendLine($"public class {className} : DbContext");
        sb.AppendLine("{");

        // Private constructor
        sb.AppendLine($"    private {className}(string connectionString) : base(connectionString)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();

        // CreateAsync method with embedded connection string
        sb.AppendLine($"    public static Task<{className}> CreateAsync()");
        sb.AppendLine("    {");
        sb.AppendLine($"        const string connectionString = \"{EscapeConnectionString(connectionString)}\";");
        sb.AppendLine($"        return CreateAsync<{className}>(connectionString);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Overload for custom connection string
        sb.AppendLine($"    public static Task<{className}> CreateAsync(string connectionString)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return CreateAsync<{className}>(connectionString);");
        sb.AppendLine("    }");

        // DbSet properties for each entity
        if (entityClassNames.Length > 0)
        {
            sb.AppendLine();
            foreach (var entityClassName in entityClassNames.OrderBy(n => n))
            {
                var propertyName = Pluralize(entityClassName);
                sb.AppendLine($"    public DbSet<{entityClassName}> {propertyName} => Set<{entityClassName}>();");
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeConnectionString(string connectionString)
    {
        return connectionString.Replace("\\", @"\\").Replace("\"", "\\\"");
    }

    private static string Pluralize(string word)
    {
        // Simple pluralization rules
        if (word.EndsWith('s') || word.EndsWith('x') || word.EndsWith('z') ||
            word.EndsWith("ch") || word.EndsWith("sh"))
        {
            return word + "es";
        }

        if (word.EndsWith('y') && word.Length > 1 && !IsVowel(word[^2]))
        {
            return word[..^1] + "ies";
        }

        if (word.EndsWith('f'))
        {
            return word[..^1] + "ves";
        }

        if (word.EndsWith("fe"))
        {
            return word[..^2] + "ves";
        }

        return word + "s";
    }

    private static bool IsVowel(char c)
    {
        return "aeiouAEIOU".Contains(c);
    }
}