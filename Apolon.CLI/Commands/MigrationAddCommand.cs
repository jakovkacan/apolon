using System.CommandLine;
using System.CommandLine.Invocation;
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

        var modelsPathOption = new Option<string>(
            aliases: ["--models-path", "-m"],
            getDefaultValue: () => "./Models",
            description: "Path to the models directory or assembly");

        var migrationsPathOption = new Option<string>(
            aliases: ["--migrations-path", "-o"],
            getDefaultValue: () => "./Migrations",
            description: "Output path for generated migrations");

        var connectionStringOption = new Option<string>(
            aliases: ["--connection-string", "-c"],
            description: "Database connection string")
        {
            IsRequired = true
        };

        var namespaceOption = new Option<string>(
            aliases: ["--namespace", "-n"],
            getDefaultValue: () => "Migrations",
            description: "Namespace for the generated migration class");

        command.AddArgument(migrationNameArg);
        command.AddOption(modelsPathOption);
        command.AddOption(migrationsPathOption);
        command.AddOption(connectionStringOption);
        command.AddOption(namespaceOption);

        command.SetHandler(async (InvocationContext context) =>
        {
            var migrationName = context.ParseResult.GetValueForArgument(migrationNameArg);
            var modelsPath = context.ParseResult.GetValueForOption(modelsPathOption)!;
            var migrationsPath = context.ParseResult.GetValueForOption(migrationsPathOption)!;
            var connectionString = context.ParseResult.GetValueForOption(connectionStringOption)!;
            var namespaceName = context.ParseResult.GetValueForOption(namespaceOption)!;

            try
            {
                var generator = new MigrationGenerator();
                var filePath = await generator.GenerateMigrationAsync(
                    migrationName,
                    modelsPath,
                    migrationsPath,
                    connectionString,
                    namespaceName);

                if (string.IsNullOrEmpty(filePath))
                {
                    context.ExitCode = 0;
                }
                else
                {
                    Console.WriteLine("\n✓ Migration generation completed successfully!");
                    context.ExitCode = 0;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"\n✗ Error: {ex.Message}");
                Console.ResetColor();

                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                context.ExitCode = 1;
            }
        });

        return command;
    }
}
