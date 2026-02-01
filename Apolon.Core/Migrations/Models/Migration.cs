namespace Apolon.Core.Migrations.Models;

public abstract class Migration
{
    public abstract void Up(MigrationBuilder migrationBuilder);
    public abstract void Down(MigrationBuilder migrationBuilder);
}