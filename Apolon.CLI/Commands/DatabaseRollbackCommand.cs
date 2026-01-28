using System.CommandLine;
using System.CommandLine.Invocation;
using Apolon.CLI.Services;

namespace Apolon.CLI.Commands;

internal static class DatabaseRollbackCommand
{
    public static Command Create()
    {
        var command = new Command("rollback", "Rollback migrations to a specific target");

        var targetMigrationArg = new Argument<string>(
            "target-migration",
            "Target migration name to rollback to (will rollback all migrations after this one)");

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

                Console.WriteLine("Database Rollback");
                Console.WriteLine("=================");
                Console.WriteLine($"Connection: {MaskConnectionString(connectionString)}");
                Console.WriteLine($"Migrations: {migrationsPath}");
                Console.WriteLine($"Target: {targetMigration}");
                Console.WriteLine();
                
                await MigrationExecutor.RollbackMigrationsAsync(
                    connectionString,
                    migrationsPath,
                    targetMigration);

                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                await Console.Error.WriteLineAsync($"\n✗ Error: {ex.Message}");
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
        
        if (passwordIndex >= 0)
        {
            var start = passwordIndex + "Password=".Length;
            var end = masked.IndexOf(';', start);
            
            if (end < 0)
                end = masked.Length;
            
            var passwordLength = end - start;
            masked = masked.Substring(0, start) + new string('*', Math.Min(passwordLength, 8)) + masked.Substring(end);
        }
        
        return masked;
    }
}
