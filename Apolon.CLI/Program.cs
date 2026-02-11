using System.CommandLine;
using Apolon.CLI.Commands;

var rootCommand = new RootCommand("Apolon - ORM management tool");

rootCommand.AddCommand(InitCommand.Create());

var migrationCommand = new Command("migration", "Manage database migrations");
migrationCommand.AddCommand(MigrationAddCommand.Create());
rootCommand.AddCommand(migrationCommand);

var databaseCommand = new Command("database", "Manage database schema");
databaseCommand.AddCommand(DatabaseUpdateCommand.Create());
databaseCommand.AddCommand(DatabaseRollbackCommand.Create());
rootCommand.AddCommand(databaseCommand);

return await rootCommand.InvokeAsync(args);