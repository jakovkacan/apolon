using System.Reflection;
using System.Text.RegularExpressions;
using Apolon.Core.Attributes;

namespace Apolon.CLI.Services;

internal static class TypeDiscovery
{
    public static Type[] DiscoverEntityTypes(string path, bool hasTableAttribute = false)
    {
        // Check if path is a .dll file
        if (File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return DiscoverEntityTypesFromAssembly(path, hasTableAttribute);
        }

        // Check if it's a directory
        if (Directory.Exists(path))
        {
            // First try to find .cs files (source-first approach)
            var csFiles = Directory.GetFiles(path, "*.cs", SearchOption.TopDirectoryOnly);
            if (csFiles.Length > 0 && (!hasTableAttribute || csFiles.Any(HasTableAttribute)))
            {
                return DiscoverEntityTypesFromSourceWithCompilation(path, hasTableAttribute);
            }

            // Fallback to looking for compiled assemblies
            return DiscoverEntityTypesFromDirectory(path, hasTableAttribute);
        }

        throw new InvalidOperationException($"Path not found or invalid: {path}");
    }

    private static bool HasTableAttribute(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Regex.IsMatch(content, @"\[Table[(\[]");
    }

    private static Type[] DiscoverEntityTypesFromSourceWithCompilation(string directoryPath,
        bool hasTableAttribute = false)
    {
        Console.WriteLine($"Found .cs files in {directoryPath}. Building project to discover types...");

        // Find the .csproj file
        var projectFiles = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories)
            .OrderBy(p => p.Length) // Prefer closest project file
            .ToArray();

        if (projectFiles.Length == 0)
        {
            // Look one level up
            var parentDir = Directory.GetParent(directoryPath);
            if (parentDir != null)
            {
                projectFiles = Directory.GetFiles(parentDir.FullName, "*.csproj", SearchOption.TopDirectoryOnly);
            }
        }

        if (projectFiles.Length == 0)
            throw new InvalidOperationException($"No .csproj file found for source directory: {directoryPath}");

        var projectFile = projectFiles[0];
        Console.WriteLine($"Building project: {projectFile}");

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

        if (buildProcess?.ExitCode != 0)
        {
            var error = buildProcess.StandardError.ReadToEnd();
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
                var types = DiscoverEntityTypesFromAssembly(assemblyFile, hasTableAttribute: hasTableAttribute);
                if (types.Length > 0)
                    return types;
            }
            catch
            {
                // Continue to next assembly
            }
        }

        throw new InvalidOperationException(
            $"No entity types with [Table] attribute found after building: {projectFile}");
    }

    private static Type[] DiscoverEntityTypesFromDirectory(string directoryPath, bool hasTableAttribute = false)
    {
        // Find all .dll files in the directory
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
            ? throw new InvalidOperationException($"No entity types with [Table] attribute found in: {directoryPath}")
            : entityTypes.ToArray();
    }

    private static Type[] DiscoverEntityTypesFromAssembly(string assemblyPath, bool hasTableAttribute = false)
    {
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly not found: {assemblyPath}");

        var assembly = Assembly.LoadFrom(assemblyPath);
        var types = assembly.GetTypes()
            .Where(t => !hasTableAttribute || t.GetCustomAttribute<TableAttribute>() != null)
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .ToArray();

        return types.Length == 0
            ? throw new InvalidOperationException($"No entity types with [Table] attribute found in: {assemblyPath}")
            : types;
    }
}