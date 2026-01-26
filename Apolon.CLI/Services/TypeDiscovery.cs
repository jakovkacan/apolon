using System.Reflection;
using Apolon.Core.Attributes;

namespace Apolon.CLI.Services;

internal static class TypeDiscovery
{
    public static Type[] DiscoverEntityTypes(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Models directory not found: {directoryPath}");

        // Find all .dll files in the directory
        var assemblyFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.AllDirectories);

        if (assemblyFiles.Length == 0)
        {
            // Try to find the project file and build it
            var projectFiles = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.TopDirectoryOnly);
            if (projectFiles.Length > 0)
            {
                throw new InvalidOperationException(
                    $"No compiled assemblies found. Please build the project first: {projectFiles[0]}");
            }
            throw new InvalidOperationException($"No assemblies or project files found in: {directoryPath}");
        }

        var entityTypes = new List<Type>();

        foreach (var assemblyFile in assemblyFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyFile);
                var types = assembly.GetTypes()
                    .Where(t => t.GetCustomAttribute<TableAttribute>() != null)
                    .Where(t => t.IsClass && !t.IsAbstract)
                    .ToArray();

                entityTypes.AddRange(types);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load assembly {Path.GetFileName(assemblyFile)}: {ex.Message}");
            }
        }

        if (entityTypes.Count == 0)
            throw new InvalidOperationException($"No entity types with [Table] attribute found in: {directoryPath}");

        return entityTypes.ToArray();
    }

    public static Type[] DiscoverEntityTypesFromAssembly(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly not found: {assemblyPath}");

        var assembly = Assembly.LoadFrom(assemblyPath);
        var types = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<TableAttribute>() != null)
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToArray();

        if (types.Length == 0)
            throw new InvalidOperationException($"No entity types with [Table] attribute found in: {assemblyPath}");

        return types;
    }
}
