using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Apolon.Core.Attributes;
using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;

namespace Apolon.CLI.Services;

internal static class TypeDiscovery
{
    public static async Task<Type[]> DiscoverEntityTypes(string path, bool hasTableAttribute = false,
        bool rebuildProject = true)
    {
        // Always try to find and build the project first to ensure latest state
        string? projectPath = null;

        // Check if path is a .dll file
        if (File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            projectPath = FindProjectFileForPath(path);
        // Check if it's a directory
        else if (Directory.Exists(path))
            projectPath = FindProjectFileForPath(path);
        else
            throw new InvalidOperationException($"Path not found or invalid: {path}");

        if (projectPath != null)
        {
            if (!rebuildProject) return LoadTypes(projectPath, hasTableAttribute);

            await BuildTypes(projectPath);
            return LoadTypes(projectPath, hasTableAttribute);
        }

        // Fallback: no project file found, load from existing assemblies
        Console.WriteLine("Warning: No project file found. Loading from existing assemblies without rebuilding.");
        if (File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return LoadTypesFromAssembly(path, hasTableAttribute);

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
            if (projectFiles.Length > 0) return projectFiles[0].FullName;

            currentDir = currentDir.Parent;
        }

        return null;
    }

    private static async Task BuildTypes(string projectFile)
    {
        Console.WriteLine($"Rebuilding project to ensure latest state: {projectFile}");

        // Build the project
        var buildProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{projectFile}\" --nologo --verbosity quiet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (buildProcess is null)
            throw new InvalidOperationException($"Failed to start dotnet process: {projectFile}");

        await buildProcess.WaitForExitAsync();

        if (buildProcess is not { ExitCode: 0 })
        {
            var error = await buildProcess.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Failed to build project: {projectFile}\n{error}");
        }

        Console.WriteLine("Build successful. Loading types from compiled assembly...");
    }

    private static Type[] LoadTypes(string projectFile, bool hasTableAttribute)
    {
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
            .Where(t => t.Name != "MigrationHistoryTable")
            .ToArray();

        return types.Length == 0
            ? throw new InvalidOperationException($"No entity types found in: {assemblyPath}")
            : types;
    }

    private static Type[] LoadTypesFromDirectory(string directoryPath, bool hasTableAttribute)
    {
        var assemblyFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.AllDirectories);

        if (assemblyFiles.Length == 0) throw new InvalidOperationException($"No assemblies found in: {directoryPath}");

        var entityTypes = new List<Type>();

        foreach (var assemblyFile in assemblyFiles)
            try
            {
                var assembly = Assembly.LoadFrom(assemblyFile);
                var types = assembly.GetTypes()
                    .Where(t => !hasTableAttribute || t.GetCustomAttribute<TableAttribute>() != null)
                    .Where(t => t is { IsClass: true, IsAbstract: false })
                    .ToArray();

                entityTypes.AddRange(types);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load assembly {Path.GetFileName(assemblyFile)}: {ex.Message}");
            }

        return entityTypes.Count == 0
            ? throw new InvalidOperationException($"No entity types found in: {directoryPath}")
            : entityTypes.ToArray();
    }

    internal static async Task<(Type Type, string Timestamp, string Name)[]> DiscoverMigrationTypes(
        string migrationsPath, bool rebuildProject = true)
    {
        var fullPath = Path.GetFullPath(migrationsPath);

        if (!Directory.Exists(fullPath))
        {
            Console.WriteLine($"Migrations directory not found: {fullPath}");
            return [];
        }

        var csFiles = Directory.GetFiles(fullPath, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith(".Designer.cs") && !f.EndsWith(".g.cs"))
            .ToArray();

        if (csFiles.Length == 0)
        {
            Console.WriteLine("No migration files found.");
            return [];
        }

        Console.WriteLine($"Found {csFiles.Length} migration file(s)");

        // Compile migration files in-memory using Roslyn
        var assembly = CompileMigrationsAsync(csFiles);

        // Extract migrations from compiled assembly
        var migrations = assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => ParseMigrationFromFile(t, csFiles))
            .Where(m => m.Type != null)
            .OrderBy(m => m.Timestamp)
            .ToArray();

        Console.WriteLine($"Loaded {migrations.Length} migration type(s)");
        return migrations;
    }

    private static (Type Type, string Timestamp, string Name) ParseMigrationFromFile(Type type, string[] csFiles)
    {
        var className = type.Name;

        // Find matching file: YYYYMMDDHHMMSS_ClassName.cs
        var matchingFile = csFiles.FirstOrDefault(f =>
        {
            var fileName = Path.GetFileNameWithoutExtension(f);
            return fileName.Length > 15 &&
                   fileName[14] == '_' &&
                   fileName[15..] == className;
        });

        if (matchingFile != null)
        {
            var fileName = Path.GetFileNameWithoutExtension(matchingFile);
            var timestamp = fileName[..14];
            return (type, timestamp, className);
        }

        return (type, "00000000000000", className);
    }

    private static Assembly CompileMigrationsAsync(string[] migrationFiles)
    {
        var systemAssemblyLocation = typeof(object).Assembly.Location;
        var systemDir = Path.GetDirectoryName(systemAssemblyLocation)!;

        var references = new List<MetadataReference>
        {
            CreateReference("System.Private.CoreLib.dll"),
            CreateReference("System.Runtime.dll"),
            CreateReference("System.Console.dll"),
            CreateReference("System.Linq.dll"),
            CreateReference("System.Linq.Expressions.dll"),
            CreateReference("System.Collections.dll"),
            MetadataReference.CreateFromFile(typeof(Migration).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(MigrationBuilder).Assembly.Location),
        };

        // Add implicit global usings (like .NET 9 does)
        const string globalUsings = @"
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Linq.Expressions;
";

        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(globalUsings, path: "GlobalUsings.g.cs")
        };

        // Add migration files
        syntaxTrees.AddRange(migrationFiles.Select(file =>
            CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file)));

        var compilation = CSharpCompilation.Create(
            assemblyName: $"Migrations_{Guid.NewGuid():N}",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = string.Join("\n", result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d =>
                    $"{d.Location.GetLineSpan().Path}({d.Location.GetLineSpan().StartLinePosition.Line + 1}): {d.GetMessage()}"));

            throw new InvalidOperationException($"Failed to compile migrations:\n{errors}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());

        MetadataReference CreateReference(string fileName)
        {
            var path = Path.Combine(systemDir, fileName);
            return !File.Exists(path)
                ? throw new FileNotFoundException($"Required assembly not found: {path}")
                : MetadataReference.CreateFromFile(path);
        }
    }

    private static (Type Type, string Timestamp, string Name) ParseMigrationNameFromType(
        Type type,
        Dictionary<string, string> classNameToTimestamp)
    {
        var className = type.Name;

        // Try to find timestamp from filename mapping
        if (classNameToTimestamp.TryGetValue(className, out var timestamp)) return (type, timestamp, className);

        // Fallback: class name might have timestamp (old format)
        return ParseMigrationNameFromClassName(type);
    }

    private static (Type Type, string Timestamp, string Name) ParseMigrationNameFromClassName(Type type)
    {
        var className = type.Name;

        // Try format: YYYYMMDDHHMMSS_MigrationName (shouldn't happen with C# classes)
        var parts = className.Split('_', 2);
        if (parts is [{ Length: 14 }, _] && long.TryParse(parts[0], out _)) return (type, parts[0], parts[1]);

        // Try format with leading underscore: _YYYYMMDDHHMMSS_MigrationName
        if (className.StartsWith('_'))
        {
            parts = className[1..].Split('_', 2);
            if (parts is [{ Length: 14 }, _] && long.TryParse(parts[0], out _)) return (type, parts[0], parts[1]);
        }

        // Fallback: no timestamp found, use zeros
        return (type, "00000000000000", className);
    }
}