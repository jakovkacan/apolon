using System.Reflection;
using System.Text.RegularExpressions;
using Apolon.Core.Attributes;
using Apolon.Core.Migrations.Models;

namespace Apolon.CLI.Services;

internal static class TypeDiscovery
{
    public static Type[] DiscoverEntityTypes(string path, bool hasTableAttribute = false)
    {
        // Always try to find and build the project first to ensure latest state
        string? projectPath = null;

        // Check if path is a .dll file
        if (File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            projectPath = FindProjectFileForPath(path);
        }
        // Check if it's a directory
        else if (Directory.Exists(path))
        {
            projectPath = FindProjectFileForPath(path);
        }
        else
        {
            throw new InvalidOperationException($"Path not found or invalid: {path}");
        }

        // If we found a project file, always rebuild it
        if (projectPath != null)
        {
            return BuildAndLoadTypes(projectPath, hasTableAttribute);
        }

        // Fallback: no project file found, load from existing assemblies
        Console.WriteLine("Warning: No project file found. Loading from existing assemblies without rebuilding.");
        if (File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return LoadTypesFromAssembly(path, hasTableAttribute);
        }

        return LoadTypesFromDirectory(path, hasTableAttribute);
    }

    private static string? FindProjectFileForPath(string path)
    {
        var searchDir = File.Exists(path) ? Path.GetDirectoryName(path) : path;

        if (string.IsNullOrEmpty(searchDir))
            return null;

        var currentDir = new DirectoryInfo(searchDir);

        // Search current directory and parent directories
        while (currentDir is { Exists: true })
        {
            var projectFiles = currentDir.GetFiles("*.csproj", SearchOption.TopDirectoryOnly);
            if (projectFiles.Length > 0)
            {
                return projectFiles[0].FullName;
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }

    private static Type[] BuildAndLoadTypes(string projectFile, bool hasTableAttribute)
    {
        Console.WriteLine($"Rebuilding project to ensure latest state: {projectFile}");

        // Build the project
        var buildProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{projectFile}\" --nologo --verbosity quiet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        buildProcess?.WaitForExit();

        if (buildProcess is not { ExitCode: 0 })
        {
            var error = buildProcess!.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Failed to build project: {projectFile}\n{error}");
        }

        Console.WriteLine("Build successful. Loading types from compiled assembly...");

        // Find the output assembly
        var projectDir = Path.GetDirectoryName(projectFile)!;
        var assemblyFiles = Directory.GetFiles(projectDir, "*.dll", SearchOption.AllDirectories)
            .Where(f => f.Contains("bin"))
            .Where(f => !f.Contains("ref")) // Exclude reference assemblies
            .OrderByDescending(File.GetLastWriteTime)
            .ToArray();

        if (assemblyFiles.Length == 0)
            throw new InvalidOperationException($"No compiled assembly found after build in: {projectDir}");

        // Try each assembly until we find types
        foreach (var assemblyFile in assemblyFiles)
        {
            try
            {
                var types = LoadTypesFromAssembly(assemblyFile, hasTableAttribute);
                if (types.Length > 0)
                    return types;
            }
            catch
            {
                // Continue to next assembly
            }
        }

        throw new InvalidOperationException(
            $"No entity types found after building: {projectFile}");
    }

    private static Type[] LoadTypesFromAssembly(string assemblyPath, bool hasTableAttribute)
    {
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly not found: {assemblyPath}");

        var assembly = Assembly.LoadFrom(assemblyPath);
        var types = assembly.GetTypes()
            .Where(t => !hasTableAttribute || t.GetCustomAttribute<TableAttribute>() != null)
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .ToArray();

        return types.Length == 0
            ? throw new InvalidOperationException($"No entity types found in: {assemblyPath}")
            : types;
    }

    private static Type[] LoadTypesFromDirectory(string directoryPath, bool hasTableAttribute)
    {
        var assemblyFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.AllDirectories);

        if (assemblyFiles.Length == 0)
        {
            throw new InvalidOperationException($"No assemblies found in: {directoryPath}");
        }

        var entityTypes = new List<Type>();

        foreach (var assemblyFile in assemblyFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyFile);
                var types = assembly.GetTypes()
                    .Where(t => (!hasTableAttribute || t.GetCustomAttribute<TableAttribute>() != null))
                    .Where(t => t is { IsClass: true, IsAbstract: false })
                    .ToArray();

                entityTypes.AddRange(types);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load assembly {Path.GetFileName(assemblyFile)}: {ex.Message}");
            }
        }

        return entityTypes.Count == 0
            ? throw new InvalidOperationException($"No entity types found in: {directoryPath}")
            : entityTypes.ToArray();
    }

    internal static (Type Type, string Timestamp, string Name)[] DiscoverMigrationTypes(string migrationsPath)
    {
        var fullPath = Path.GetFullPath(migrationsPath);

        // Check if directory exists
        if (!Directory.Exists(fullPath))
        {
            Console.WriteLine($"Migrations directory not found: {fullPath}");
            return [];
        }

        // Check for .cs files (auto-build if needed)
        var csFiles = Directory.GetFiles(fullPath, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith(".Designer.cs") && !f.EndsWith(".g.cs"))
            .ToArray();

        if (csFiles.Length > 0)
        {
            // Build the project to get types
            var types = DiscoverEntityTypes(fullPath);

            Console.WriteLine($"Found {types.Length} type(s) in source files.");

            // Create a mapping of class names to their source files
            var fileNameToTimestamp = csFiles
                .Select(f => new
                {
                    FileName = Path.GetFileNameWithoutExtension(f),
                    FilePath = f
                })
                .Where(x => x.FileName.Length > 15 && x.FileName[14] == '_') // YYYYMMDDHHMMSS_
                .ToDictionary(
                    x => x.FileName[15..], // Class name after timestamp
                    x => x.FileName[..14]); // Timestamp

            Console.WriteLine($"Found {fileNameToTimestamp.Count} migration(s) in source files.");

            // Filter for Migration types and extract timestamp/name from filename
            return types
                .Where(t => typeof(Migration).IsAssignableFrom(t))
                .Select(t => ParseMigrationNameFromType(t, fileNameToTimestamp))
                .Where(m => m.Type != null)
                .OrderBy(m => m.Timestamp)
                .ToArray();
        }

        // Try to load from compiled assemblies in the directory
        var dllFiles = Directory.GetFiles(fullPath, "*.dll", SearchOption.AllDirectories);
        var migrationTypes = new List<(Type Type, string Timestamp, string Name)>();

        foreach (var dllFile in dllFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllFile);
                var types = assembly.GetTypes()
                    .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
                    .Select(ParseMigrationNameFromClassName)
                    .Where(m => m.Type != null);

                migrationTypes.AddRange(types);
            }
            catch
            {
                // Ignore assemblies that can't be loaded
            }
        }

        return migrationTypes.OrderBy(m => m.Timestamp).ToArray();
    }

    private static (Type Type, string Timestamp, string Name) ParseMigrationNameFromType(
        Type type,
        Dictionary<string, string> classNameToTimestamp)
    {
        var className = type.Name;

        // Try to find timestamp from filename mapping
        if (classNameToTimestamp.TryGetValue(className, out var timestamp))
        {
            return (type, timestamp, className);
        }

        // Fallback: class name might have timestamp (old format)
        return ParseMigrationNameFromClassName(type);
    }

    private static (Type Type, string Timestamp, string Name) ParseMigrationNameFromClassName(Type type)
    {
        var className = type.Name;

        // Try format: YYYYMMDDHHMMSS_MigrationName (shouldn't happen with C# classes)
        var parts = className.Split('_', 2);
        if (parts is [{ Length: 14 }, _] && long.TryParse(parts[0], out _))
        {
            return (type, parts[0], parts[1]);
        }

        // Try format with leading underscore: _YYYYMMDDHHMMSS_MigrationName
        if (className.StartsWith("_"))
        {
            parts = className.Substring(1).Split('_', 2);
            if (parts is [{ Length: 14 }, _] && long.TryParse(parts[0], out _))
            {
                return (type, parts[0], parts[1]);
            }
        }

        // Fallback: no timestamp found, use zeros
        return (type, "00000000000000", className);
    }
}