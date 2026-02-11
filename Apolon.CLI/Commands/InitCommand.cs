using System.CommandLine;
using Apolon.CLI.Services;

namespace Apolon.CLI.Commands;

internal static class InitCommand
{
    public static Command Create()
    {
        var command = new Command("init", "Initialize a new DbContext for the specified Models folder");

        var connectionStringArg = new Argument<string>(
            "connection-string",
            "Database connection string");

        var modelsPathOption = new Option<string>(
            ["--models-path", "-m"],
            () => ".\\Models",
            "Path to the models directory or assembly");

        var outputPathOption = new Option<string?>(
            ["--output-path", "-o"],
            () => null,
            "Output path for the DbContext file (if not specified, will be placed in the models directory)");

        var rootNamespace = new Option<string?>(
            ["--root-namespace", "-N"],
            () => null,
            "Root namespace of the project");

        var dbContextNamespace = new Option<string?>(
            ["--namespace", "-n"],
            () => null,
            "Namespace for the generated DbContext class (if not specified, will be inferred from output directory)");

        command.AddArgument(connectionStringArg);
        command.AddOption(modelsPathOption);
        command.AddOption(outputPathOption);
        command.AddOption(dbContextNamespace);
        command.AddOption(rootNamespace);

        command.SetHandler(async context =>
        {
            var connectionString = context.ParseResult.GetValueForArgument(connectionStringArg);
            var modelsPath = context.ParseResult.GetValueForOption(modelsPathOption)!;
            var outputPath = context.ParseResult.GetValueForOption(outputPathOption);
            var dbContextNamespaceName = context.ParseResult.GetValueForOption(dbContextNamespace);
            var rootNamespaceName = context.ParseResult.GetValueForOption(rootNamespace);

            try
            {
                var filePath = await DbContextGenerator.GenerateDbContextAsync(
                    connectionString,
                    modelsPath,
                    outputPath,
                    dbContextNamespaceName,
                    rootNamespaceName);

                Console.WriteLine($"\nDbContext generated successfully at: {filePath}");

                // Save configuration
                var finalOutputPath = outputPath ?? modelsPath;
                var rootDir = new DirectoryInfo(finalOutputPath).Parent?.FullName;
                var finalNamespace = rootNamespaceName ?? new DirectoryInfo(Path.GetFullPath(finalOutputPath)).Name;

                var config = new ProjectConfiguration
                {
                    ConnectionString = connectionString,
                    ModelsPath = Path.GetFullPath(modelsPath),
                    DbContextPath = Path.GetFullPath(filePath),
                    MigrationsPath = Path.Combine(rootDir ?? Path.GetFullPath(finalOutputPath), "Migrations"),
                    Namespace = finalNamespace,
                    InitializedAt = DateTime.UtcNow
                };

                await ProjectConfiguration.SaveAsync(config, Directory.GetCurrentDirectory());
                Console.WriteLine("Configuration saved to .apolon.json");

                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                await Console.Error.WriteLineAsync($"\nError: {ex.Message}");
                Console.ResetColor();

                if (ex.InnerException != null)
                    await Console.Error.WriteLineAsync($"Inner exception: {ex.InnerException.Message}");

                context.ExitCode = 1;
            }
        });

        return command;
    }
}