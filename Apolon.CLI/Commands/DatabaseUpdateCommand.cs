using System.CommandLine;
using System.CommandLine.Invocation;
using Apolon.CLI.Services;

namespace Apolon.CLI.Commands;

internal static class DatabaseUpdateCommand
{
    public static Command Create()
    {
        var command = new Command("update", "Apply pending migrations to the database or rollback to a specific migration");

        var targetMigrationArg = new Argument<string?>(
            "target-migration",
            () => null,
            "Target migration name (optional). If specified and older than current, will rollback. If newer or not applied, will apply up to that migration.");

        var connectionStringOption = new Option<string?>(
            aliases: ["--connection-string", "-c"],
            getDefaultValue: () => null,
            description: "Database connection string (defaults to config)");

        var migrationsPathOption = new Option<string?>(
            aliases: ["--migrations-path", "-m"],
            getDefaultValue: () => null,
            description: "Path to migrations directory (defaults to config)");

        command.AddArgument(targetMigrationArg);
        command.AddOption(connectionStringOption);
        command.AddOption(migrationsPathOption);

        command.SetHandler(async (InvocationContext context) =>
        {
            var targetMigration = context.ParseResult.GetValueForArgument(targetMigrationArg);
            var connectionStringOverride = context.ParseResult.GetValueForOption(connectionStringOption);
            var migrationsPathOverride = context.ParseResult.GetValueForOption(migrationsPathOption);

            try
            {
                // Load configuration
                var config = await ProjectConfiguration.LoadOrThrowAsync(Directory.GetCurrentDirectory());

                var connectionString = connectionStringOverride ?? config.ConnectionString;
                var migrationsPath = migrationsPathOverride ?? config.MigrationsPath;

                Console.WriteLine("Database Update");
                Console.WriteLine("===============");
                Console.WriteLine($"Connection: {MaskConnectionString(connectionString)}");
                Console.WriteLine($"Migrations: {migrationsPath}");
                Console.WriteLine();

                // Determine if this is a rollback or forward migration
                if (!string.IsNullOrEmpty(targetMigration))
                {
                    // Check if target is already applied (rollback scenario)
                    // This will be determined inside the executor
                    await MigrationExecutor.ExecuteMigrationsAsync(
                        connectionString,
                        migrationsPath,
                        targetMigration);
                }
                else
                {
                    // Apply all pending migrations
                    await MigrationExecutor.ExecuteMigrationsAsync(
                        connectionString,
                        migrationsPath);
                }

                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                await Console.Error.WriteLineAsync($"\nError: {ex.Message}");
                Console.ResetColor();

                if (ex.InnerException != null)
                {
                    await Console.Error.WriteLineAsync($"Inner exception: {ex.InnerException.Message}");
                }

                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Mask password in connection string for display
        var masked = connectionString;
        var passwordIndex = masked.IndexOf("Password=", StringComparison.OrdinalIgnoreCase);

        if (passwordIndex < 0) return masked;
        
        var start = passwordIndex + "Password=".Length;
        var end = masked.IndexOf(';', start);
            
        if (end < 0)
            end = masked.Length;
            
        var passwordLength = end - start;
        masked = string.Concat(masked.AsSpan()[..start], new string('*', Math.Min(passwordLength, 8)), masked.AsSpan(end));

        return masked;
    }
}
