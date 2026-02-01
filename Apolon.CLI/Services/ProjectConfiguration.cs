using System.Text.Json;

namespace Apolon.CLI.Services;

internal class ProjectConfiguration
{
    private const string ConfigFileName = ".apolon.json";

    public string ConnectionString { get; set; } = string.Empty;
    public string ModelsPath { get; set; } = string.Empty;
    public string DbContextPath { get; set; } = string.Empty;
    public string MigrationsPath { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public DateTime InitializedAt { get; set; }

    public static async Task SaveAsync(ProjectConfiguration config, string directory = ".")
    {
        var configPath = Path.Combine(directory, ConfigFileName);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(configPath, json);
    }

    public static async Task<ProjectConfiguration?> LoadAsync(string directory = ".")
    {
        var configPath = FindConfigFile(directory);

        if (configPath == null)
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            return JsonSerializer.Deserialize<ProjectConfiguration>(json);
        }
        catch
        {
            return null;
        }
    }

    public static bool Exists(string directory = ".")
    {
        return FindConfigFile(directory) != null;
    }

    private static string? FindConfigFile(string directory)
    {
        var currentDir = new DirectoryInfo(Path.GetFullPath(directory));

        // Search up to 3 levels up
        for (var i = 0; i < 3 && currentDir != null; i++)
        {
            var configPath = Path.Combine(currentDir.FullName, ConfigFileName);
            if (File.Exists(configPath))
                return configPath;

            currentDir = currentDir.Parent;
        }

        return null;
    }

    public static async Task<ProjectConfiguration> LoadOrThrowAsync(string directory = ".")
    {
        var config = await LoadAsync(directory);

        if (config == null)
            throw new InvalidOperationException(
                $"Project not initialized. Please run 'apolon init <connection-string>' first.\n" +
                $"The init command creates a {ConfigFileName} file with your database configuration.");

        return config;
    }
}