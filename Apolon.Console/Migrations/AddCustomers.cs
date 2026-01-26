using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;

namespace Apolon.Console.Migrations;

public sealed class AddCustomers : Migration
{
    public override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateSchema("public");
        migrationBuilder.CreateTable("public", "Customers");
        migrationBuilder.AddColumn("public", "Customers", "Id", "int", false, isPrimaryKey: true, isIdentity: true);
        migrationBuilder.AddColumn("public", "Customers", "Name", "varchar(255)", false);
        migrationBuilder.AddColumn("public", "Customers", "Email", "varchar(255)", true);
    }

    public override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("public", "Customers");
    }
}