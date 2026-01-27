using System.Reflection;
using System.Text.RegularExpressions;
using Apolon.Core.Attributes;

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
}