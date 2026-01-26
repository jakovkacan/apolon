using System.Text;
using Apolon.Core.Migrations.Models;

namespace Apolon.CLI.Services;

internal static class MigrationCodeGenerator
{
    public static string GenerateMigrationCode(
        string migrationName,
        IReadOnlyList<MigrationOperation> operations,
        string namespaceName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var className = $"{timestamp}_{migrationName}";

        var sb = new StringBuilder();
        sb.AppendLine("using Apolon.Core.Migrations;");
        sb.AppendLine("using Apolon.Core.Migrations.Models;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {className} : Migration");
        sb.AppendLine("{");

        // Generate Up() method
        sb.AppendLine("    public override void Up(MigrationBuilder migrationBuilder)");
        sb.AppendLine("    {");
        GenerateOperations(sb, operations, indent: "        ");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate Down() method
        sb.AppendLine("    public override void Down(MigrationBuilder migrationBuilder)");
        sb.AppendLine("    {");
        GenerateReverseOperations(sb, operations, indent: "        ");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateOperations(StringBuilder sb, IReadOnlyList<MigrationOperation> operations, string indent)
    {
        // Track unique schemas to avoid duplicate CreateSchema calls
        var createdSchemas = new HashSet<string>();

        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case MigrationOperationType.CreateSchema:
                    if (createdSchemas.Add(op.Schema))
                        sb.AppendLine($"{indent}migrationBuilder.CreateSchema(\"{op.Schema}\");");
                    break;

                case MigrationOperationType.CreateTable:
                    sb.AppendLine($"{indent}migrationBuilder.CreateTable(\"{op.Schema}\", \"{op.Table}\");");
                    break;

                case MigrationOperationType.AddColumn:
                    var addColumnCall = GenerateAddColumnCall(op);
                    sb.AppendLine($"{indent}{addColumnCall}");
                    break;

                case MigrationOperationType.DropTable:
                    sb.AppendLine($"{indent}migrationBuilder.DropTable(\"{op.Schema}\", \"{op.Table}\");");
                    break;

                case MigrationOperationType.DropColumn:
                    sb.AppendLine($"{indent}migrationBuilder.DropColumn(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\");");
                    break;

                case MigrationOperationType.AlterColumnType:
                    sb.AppendLine($"{indent}migrationBuilder.AlterColumnType(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\", \"{op.GetSqlType()}\");");
                    break;

                case MigrationOperationType.AlterNullability:
                    sb.AppendLine($"{indent}migrationBuilder.AlterNullability(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\", {(op.IsNullable!.Value ? "true" : "false")});");
                    break;

                case MigrationOperationType.SetDefault:
                    sb.AppendLine($"{indent}migrationBuilder.SetDefault(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\", \"{EscapeString(op.DefaultSql!)}\");");
                    break;

                case MigrationOperationType.DropDefault:
                    sb.AppendLine($"{indent}migrationBuilder.DropDefault(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\");");
                    break;

                case MigrationOperationType.AddUnique:
                    sb.AppendLine($"{indent}migrationBuilder.AddUnique(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\");");
                    break;

                case MigrationOperationType.DropConstraint:
                    sb.AppendLine($"{indent}migrationBuilder.DropConstraint(\"{op.Schema}\", \"{op.Table}\", \"{op.ConstraintName}\");");
                    break;

                case MigrationOperationType.AddForeignKey:
                    var fkCall = GenerateAddForeignKeyCall(op);
                    sb.AppendLine($"{indent}{fkCall}");
                    break;
            }
        }
    }

    private static void GenerateReverseOperations(StringBuilder sb, IReadOnlyList<MigrationOperation> operations, string indent)
    {
        // Reverse operations in reverse order
        var reversedOps = operations.Reverse().ToList();
        var droppedTables = new HashSet<(string Schema, string Table)>();

        foreach (var op in reversedOps)
        {
            switch (op.Type)
            {
                case MigrationOperationType.CreateTable:
                    if (droppedTables.Add((op.Schema, op.Table)))
                        sb.AppendLine($"{indent}migrationBuilder.DropTable(\"{op.Schema}\", \"{op.Table}\");");
                    break;

                case MigrationOperationType.DropTable:
                    sb.AppendLine($"{indent}// TODO: Recreate table \"{op.Schema}.{op.Table}\" if needed");
                    break;

                case MigrationOperationType.AddColumn:
                    // Only drop column if table wasn't dropped
                    if (!droppedTables.Contains((op.Schema, op.Table)))
                        sb.AppendLine($"{indent}migrationBuilder.DropColumn(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\");");
                    break;

                case MigrationOperationType.DropColumn:
                    sb.AppendLine($"{indent}// TODO: Recreate column \"{op.Schema}.{op.Table}.{op.Column}\" if needed");
                    break;

                case MigrationOperationType.AlterColumnType:
                case MigrationOperationType.AlterNullability:
                    sb.AppendLine($"{indent}// TODO: Revert column changes for \"{op.Schema}.{op.Table}.{op.Column}\" if needed");
                    break;
            }
        }
    }

    private static string GenerateAddColumnCall(MigrationOperation op)
    {
        var parts = new List<string>
        {
            $"\"{op.Schema}\"",
            $"\"{op.Table}\"",
            $"\"{op.Column}\"",
            $"\"{op.GetSqlType()}\"",
            op.IsNullable!.Value ? "true" : "false"
        };

        var namedParams = new List<string>();

        if (!string.IsNullOrWhiteSpace(op.DefaultSql))
            namedParams.Add($"defaultSql: \"{EscapeString(op.DefaultSql)}\"");

        if (op.IsPrimaryKey == true)
            namedParams.Add("isPrimaryKey: true");

        if (op.IsIdentity == true)
            namedParams.Add("isIdentity: true");

        if (!string.IsNullOrWhiteSpace(op.IdentityGeneration))
            namedParams.Add($"identityGeneration: \"{op.IdentityGeneration}\"");

        var allParams = string.Join(", ", parts.Concat(namedParams));
        return $"migrationBuilder.AddColumn({allParams});";
    }

    private static string GenerateAddForeignKeyCall(MigrationOperation op)
    {
        var parts = new List<string>
        {
            $"\"{op.Schema}\"",
            $"\"{op.Table}\"",
            $"\"{op.Column}\"",
            $"\"{op.ConstraintName}\"",
            $"\"{op.RefSchema}\"",
            $"\"{op.RefTable}\"",
            $"\"{op.RefColumn}\""
        };

        if (!string.IsNullOrWhiteSpace(op.OnDeleteRule))
            parts.Add($"\"{op.OnDeleteRule}\"");

        return $"migrationBuilder.AddForeignKey({string.Join(", ", parts)});";
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
