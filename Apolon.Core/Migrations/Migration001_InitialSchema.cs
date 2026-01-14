using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations;

public class Migration001_InitialSchema : Migration
{
    public override void Up()
    {
        // Create Patient table
        // ExecuteSql(SchemaGenerator.GenerateCreateTableSql(typeof(Patient)));
        // ExecuteSql(SchemaGenerator.GenerateCreateTableSql(typeof(Checkup)));
        // ExecuteSql(SchemaGenerator.GenerateCreateTableSql(typeof(Medication)));
        // ExecuteSql(SchemaGenerator.GenerateCreateTableSql(typeof(Prescription)));
        // ExecuteSql(SchemaGenerator.GenerateCreateTableSql(typeof(CheckupType)));
    }

    public override void Down()
    {
        ExecuteSql("DROP TABLE IF EXISTS public.prescription CASCADE;");
        ExecuteSql("DROP TABLE IF EXISTS public.checkup CASCADE;");
        ExecuteSql("DROP TABLE IF EXISTS public.medication CASCADE;");
        ExecuteSql("DROP TABLE IF EXISTS public.patient CASCADE;");
        ExecuteSql("DROP TABLE IF EXISTS public.checkup_type CASCADE;");
    }
}