using System.CommandLine;
using Apolon.CLI.Commands;

var rootCommand = new RootCommand("Apolon CLI - Database migration management tool");

var migrationCommand = new Command("migration", "Manage database migrations");
migrationCommand.AddCommand(MigrationAddCommand.Create());

rootCommand.AddCommand(migrationCommand);

return await rootCommand.InvokeAsync(args);
