# Apolon CLI - Migration Management Tool

A command-line tool for generating database migrations by comparing your entity models with the current database schema.

## Features

- **Automatic Migration Generation**: Scans your Models directory and generates migrations based on differences between models and database
- **Schema Diffing**: Uses `ModelSnapshotBuilder`, `SnapshotReader`, and `SchemaDiffer` to detect changes
- **Type-Safe**: Generates strongly-typed C# migration files that use `MigrationBuilder` API
- **Reversible**: Generates both `Up()` and `Down()` methods

## Installation

Build the project:
```bash
dotnet build Apolon.CLI/Apolon.CLI.csproj
```

## Usage

### Generate a Migration

```bash
dotnet run --project Apolon.CLI migration add <MigrationName> [options]
```

### Options

- **`<MigrationName>`** (required): Name for the migration (e.g., `AddCustomers`, `UpdatePatientSchema`)
- **`--models-path, -m`**: Path to models directory or assembly (default: `./Models`)
- **`--migrations-path, -o`**: Output directory for generated migrations (default: `./Migrations`)
- **`--connection-string, -c`** (required): Database connection string
- **`--namespace, -n`**: Namespace for generated migration class (default: `Migrations`)

### Example

Generate a migration for the Apolon.Console project:

```bash
cd Apolon.Console

dotnet run --project ../Apolon.CLI migration add InitialCreate \
  --models-path ../Apolon.Models/bin/Debug/net9.0 \
  --migrations-path ./Migrations \
  --connection-string "Host=localhost;Database=apolon;Username=postgres;Password=yourpassword" \
  --namespace Apolon.Console.Migrations
```

### Expected Output

```
Discovering entity types in: C:\...\Apolon.Models\bin\Debug\net9.0
Found 5 entity types: Patient, Checkup, Test, Medication, Prescription
Building model snapshot...
Fetching database snapshot...
Comparing snapshots...
Found 12 migration operations

Operations to be generated:
  - CreateSchema: public
  - CreateTable: public.patients
  - AddColumn: public.patients.id
  - AddColumn: public.patients.first_name
  ...

Generating migration file: InitialCreate
Migration generated successfully at: ./Migrations/20260126123045_InitialCreate.cs

✓ Migration generation completed successfully!
```

## Generated Migration Format

The tool generates migrations similar to this:

```csharp
using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;

namespace Apolon.Console.Migrations;

public sealed class 20260126123045_InitialCreate : Migration
{
    public override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateSchema("public");
        migrationBuilder.CreateTable("public", "patients");
        migrationBuilder.AddColumn("public", "patients", "id", "INT", false, isPrimaryKey: true, isIdentity: true);
        migrationBuilder.AddColumn("public", "patients", "first_name", "VARCHAR(100)", false);
        // ... more operations
    }

    public override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("public", "patients");
        // ... reverse operations
    }
}
```

## Workflow

1. **Modify your entity models** in `Apolon.Models`
2. **Build the Models project** to generate the assembly
   ```bash
   dotnet build Apolon.Models
   ```
3. **Run the CLI tool** to generate a migration
4. **Review the generated migration** file
5. **Run the migration** using `MigrationRunner.RunPendingMigrations(typeof(YourMigration))`

## How It Works

1. **Type Discovery**: Scans assemblies in the models path for classes with `[Table]` attribute
2. **Model Snapshot**: Builds a schema snapshot from entity metadata using `ModelSnapshotBuilder`
3. **Database Snapshot**: Queries the database using `SnapshotReader` to get current schema
4. **Schema Diff**: Compares both snapshots using `SchemaDiffer` to generate operations
5. **Code Generation**: Converts operations to C# code using `MigrationCodeGenerator`

## Troubleshooting

### "No compiled assemblies found"
Make sure to build your Models project first:
```bash
dotnet build Apolon.Models
```

### "No entity types with [Table] attribute found"
Ensure your entity classes are decorated with `[Table]` attribute:
```csharp
[Table("patients", Schema = "public")]
public class Patient { ... }
```

### Connection errors
Verify your connection string is correct and the database is accessible.

## Architecture

- **`Program.cs`**: CLI entry point using System.CommandLine
- **`Commands/MigrationAddCommand.cs`**: Handles the `migration add` command
- **`Services/MigrationGenerator.cs`**: Orchestrates the migration generation process
- **`Services/MigrationCodeGenerator.cs`**: Generates C# code from migration operations
- **`Services/TypeDiscovery.cs`**: Discovers entity types from assemblies
