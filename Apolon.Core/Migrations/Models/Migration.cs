using Apolon.Core.Migrations;

namespace Apolon.Core.Migrations.Models;

public abstract class Migration
{
    public abstract void Up(MigrationBuilder migrationBuilder);
    public abstract void Down(MigrationBuilder migrationBuilder);


    protected static IReadOnlyList<MigrationOperation> DiffSchema(
        SchemaSnapshot expected,
        SchemaSnapshot actual)
    {
        return SchemaDiffer.Diff(expected, actual);
    }
}
