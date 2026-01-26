using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;

namespace Apolon.Console.Migrations;

public sealed class InitialCreate : Migration
{
    public override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateSchema("public");
        migrationBuilder.CreateTable("public", "__efmigrationshistory");
        migrationBuilder.AddColumn("public", "__efmigrationshistory", "migration_id", "VARCHAR(255)", false, isPrimaryKey: true, isIdentity: true, identityGeneration: "always");
        migrationBuilder.AddColumn("public", "__efmigrationshistory", "product_version", "VARCHAR(255)", false);
        migrationBuilder.AddColumn("public", "__efmigrationshistory", "applied_at", "TIMESTAMP(6)", false);
        migrationBuilder.DropTable("public", "customers");
    }

    public override void Down(MigrationBuilder migrationBuilder)
    {
        // TODO: Recreate table "public.customers" if needed
        migrationBuilder.DropColumn("public", "__efmigrationshistory", "applied_at");
        migrationBuilder.DropColumn("public", "__efmigrationshistory", "product_version");
        migrationBuilder.DropColumn("public", "__efmigrationshistory", "migration_id");
        migrationBuilder.DropTable("public", "__efmigrationshistory");
    }
}
