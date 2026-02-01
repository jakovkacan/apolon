using System.Text.RegularExpressions;

namespace Apolon.CLI.Services;

internal static class SourceFileParser
{
    public static (string ClassName, string Namespace)[] DiscoverEntityClassesFromSource(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var csFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.TopDirectoryOnly);

        if (csFiles.Length == 0)
            throw new InvalidOperationException($"No .cs files found in: {directoryPath}");

        var entities = new List<(string ClassName, string Namespace)>();

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);

            // Check if file has [Table] attribute
            if (!Regex.IsMatch(content, @"\[Table[(\[]"))
                continue;

            // Extract namespace
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w\.]+)\s*;");
            if (!namespaceMatch.Success)
                namespaceMatch = Regex.Match(content, @"namespace\s+([\w\.]+)\s*\{");

            var namespaceName = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Models";

            // Extract class name (public class ClassName)
            var classMatch = Regex.Match(content, @"public\s+class\s+(\w+)");
            if (!classMatch.Success)
                continue;

            var className = classMatch.Groups[1].Value;
            entities.Add((className, namespaceName));
        }

        return entities.Count == 0
            ? throw new InvalidOperationException($"No entity classes with [Table] attribute found in: {directoryPath}")
            : entities.ToArray();
    }
}