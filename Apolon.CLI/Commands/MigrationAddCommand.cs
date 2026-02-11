using System.CommandLine;
using Apolon.CLI.Services;

namespace Apolon.CLI.Commands;

internal static class MigrationAddCommand
{
    public static Command Create()
    {
        var command = new Command("add", "Generate a new migration based on model changes");

        var migrationNameArg = new Argument<string>(
            "name",
            "The name of the migration (e.g., AddCustomers)");

        var modelsPathOption = new Option<string?>(
            ["--models-path", "-m"],
            () => null,
            "Path to the models directory or assembly (defaults to config)");

        var migrationsPathOption = new Option<string?>(
            ["--migrations-path", "-o"],
            () => null,
            "Output path for generated migrations (defaults to config)");

        var connectionStringOption = new Option<string?>(
            ["--connection-string", "-c"],
            () => null,
            "Database connection string (defaults to config)");

        var namespaceOption = new Option<string?>(
            ["--namespace", "-n"],
            () => null,
            "Namespace for the generated migration class (defaults to config)");

        command.AddArgument(migrationNameArg);
        command.AddOption(modelsPathOption);
        command.AddOption(migrationsPathOption);
        command.AddOption(connectionStringOption);
        command.AddOption(namespaceOption);

        command.SetHandler(async context =>
        {
            var migrationName = context.ParseResult.GetValueForArgument(migrationNameArg);
            var modelsPathOverride = context.ParseResult.GetValueForOption(modelsPathOption);
            var migrationsPathOverride = context.ParseResult.GetValueForOption(migrationsPathOption);
            var connectionStringOverride = context.ParseResult.GetValueForOption(connectionStringOption);
            var namespaceOverride = context.ParseResult.GetValueForOption(namespaceOption);

            try
            {
                // Load configuration (throws if not initialized)
                var config = await ProjectConfiguration.LoadOrThrowAsync(Directory.GetCurrentDirectory());

                // Use overrides or fall back to config
                var modelsPath = modelsPathOverride ?? config.ModelsPath;
                var migrationsPath = migrationsPathOverride ?? config.MigrationsPath;
                var connectionString = connectionStringOverride ?? config.ConnectionString;
                var namespaceName = namespaceOverride ?? (config.Namespace + ".Migrations");

                Console.WriteLine("Using configuration:");
                Console.WriteLine($"  Models: {modelsPath}");
                Console.WriteLine($"  Migrations: {migrationsPath}");
                Console.WriteLine($"  Namespace: {namespaceName}");
                Console.WriteLine();

                var filePath = await MigrationGenerator.GenerateMigrationAsync(
                    migrationName,
                    modelsPath,
                    migrationsPath,
                    connectionString,
                    namespaceName);

                if (string.IsNullOrEmpty(filePath))
                {
                }
                else
                {
                    Console.WriteLine("\nMigration generation completed successfully!");
                }

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