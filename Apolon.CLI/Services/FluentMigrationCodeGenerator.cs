using System.Text;
using System.Text.RegularExpressions;
using Apolon.Core.Migrations.Models;

namespace Apolon.CLI.Services;

/// <summary>
/// Generates migration code using the new fluent builder pattern API.
/// </summary>
internal static class FluentMigrationCodeGenerator
{
    public static string GenerateMigrationCode(
        string migrationName,
        IReadOnlyList<MigrationOperation> operations,
        string namespaceName,
        IReadOnlyList<MigrationOperation>? allCommittedOperations = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Apolon.Core.Migrations;");
        sb.AppendLine("using Apolon.Core.Migrations.Models;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {migrationName} : Migration");
        sb.AppendLine("{");

        // Generate Up() method
        sb.AppendLine("    public override void Up(MigrationBuilder migrationBuilder)");
        sb.AppendLine("    {");
        GenerateFluentOperations(sb, operations, indent: "        ");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate Down() method
        sb.AppendLine("    public override void Down(MigrationBuilder migrationBuilder)");
        sb.AppendLine("    {");
        GenerateReverseOperations(sb, operations, allCommittedOperations ?? [], indent: "        ");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateFluentOperations(
        StringBuilder sb,
        IReadOnlyList<MigrationOperation> operations,
        string indent)
    {
        // Group operations by table for CreateTable fluent API
        var tableGroups = GroupOperationsByTable(operations);

        foreach (var group in tableGroups)
        {
            switch (group.Type)
            {
                case TableOperationType.CreateTable:
                    GenerateCreateTableFluent(sb, group, indent);
                    break;

                case TableOperationType.AlterTable:
                    GenerateAlterTableOperations(sb, group.Operations, indent);
                    break;

                case TableOperationType.DropTable:
                    foreach (var op in group.Operations)
                        sb.AppendLine($"{indent}migrationBuilder.DropTable(\"{op.Schema}\", \"{op.Table}\");");
                    break;

                case TableOperationType.Other:
                    GenerateOtherOperations(sb, group.Operations, indent);
                    break;
            }
        }
    }

    private static void GenerateCreateTableFluent(
        StringBuilder sb,
        TableOperationGroup group,
        string indent)
    {
        var createOp = group.Operations.First(op => op.Type == MigrationOperationType.CreateTable);
        var columnOps = group.Operations.Where(op => op.Type == MigrationOperationType.AddColumn).ToList();
        var fkOps = group.Operations.Where(op => op.Type == MigrationOperationType.AddForeignKey).ToList();
        var uniqueOps = group.Operations.Where(op => op.Type == MigrationOperationType.AddUnique).ToList();
        var pkColumn = columnOps.FirstOrDefault(op => op.IsPrimaryKey == true);

        // Mark columns that have unique constraints
        var uniqueColumnNames = new HashSet<string>(uniqueOps.Select(u => u.Column!));

        sb.AppendLine($"{indent}migrationBuilder.CreateTable(");
        sb.AppendLine($"{indent}    name: \"{createOp.Table}\",");

        // Columns lambda
        sb.AppendLine($"{indent}    columns: table => new");
        sb.AppendLine($"{indent}    {{");

        for (int i = 0; i < columnOps.Count; i++)
        {
            var colOp = columnOps[i];
            var isLast = i == columnOps.Count - 1;
            var isUnique = uniqueColumnNames.Contains(colOp.Column!);
            GenerateColumnDefinition(sb, colOp, indent + "        ", isLast, isUnique);
        }

        sb.AppendLine($"{indent}    }},");

        // Schema parameter (if not public)
        if (createOp.Schema != "public")
        {
            sb.AppendLine($"{indent}    schema: \"{createOp.Schema}\",");
        }

        // Constraints lambda (if there's a PK, FKs, or Unique constraints)
        if (pkColumn != null || fkOps.Any() || uniqueOps.Any())
        {
            sb.AppendLine($"{indent}    constraints: table =>");
            sb.AppendLine($"{indent}    {{");

            // Primary key
            if (pkColumn != null)
            {
                sb.AppendLine(
                    $"{indent}        table.PrimaryKey(\"{createOp.Table}_pkey\", x => x.{pkColumn.Column});");
            }

            // Unique constraints
            foreach (var unique in uniqueOps)
            {
                sb.AppendLine(
                    $"{indent}        table.UniqueConstraint(\"{createOp.Table}_{unique.Column}_key\", x => x.{unique.Column});");
            }

            // Foreign keys
            foreach (var fk in fkOps)
            {
                sb.Append($"{indent}        table.ForeignKey(");
                sb.Append($"\"{fk.ConstraintName ?? $"{createOp.Table}_{fk.Column}_fkey"}\", ");
                sb.Append($"x => x.{fk.Column}, ");
                sb.Append($"\"{fk.RefTable}\", ");
                sb.Append($"\"{fk.RefColumn}\"");

                if (fk.RefSchema != null && fk.RefSchema != "public")
                    sb.Append($", principalSchema: \"{fk.RefSchema}\"");

                if (!string.IsNullOrWhiteSpace(fk.OnDeleteRule))
                    sb.Append($", onDelete: \"{fk.OnDeleteRule}\"");

                sb.AppendLine(");");
            }

            sb.Append($"{indent}    }}");
        }
        else
        {
            // Remove trailing comma
            var lastLine = sb.ToString().TrimEnd();
            sb.Clear();
            sb.Append(lastLine.TrimEnd(','));
        }

        sb.AppendLine(");");
        sb.AppendLine();
    }

    private static void GenerateColumnDefinition(
        StringBuilder sb,
        MigrationOperation colOp,
        string indent,
        bool isLast,
        bool isUnique = false)
    {
        var clrType = InferClrType(colOp.GetSqlType() ?? "TEXT");
        var nullable = colOp.IsNullable == true;

        sb.Append($"{indent}{colOp.Column} = table.Column<{clrType}>(");

        var parameters = new List<string>();

        // Type parameter (if explicit)
        if (!string.IsNullOrWhiteSpace(colOp.GetSqlType()))
            parameters.Add($"type: \"{colOp.GetSqlType()}\"");

        // Nullable parameter
        if (!nullable)
            parameters.Add("nullable: false");

        if (parameters.Any())
            sb.Append(string.Join(", ", parameters));

        sb.Append(")");

        // Fluent methods
        var fluentMethods = new List<string>();

        // Annotations (for identity)
        if (colOp.IsIdentity == true)
        {
            var identityValue = colOp.IdentityGeneration ?? "ALWAYS";
            fluentMethods.Add($".Annotation(\"Postgres:Identity\", \"{identityValue}\")");
        }

        // Default value
        if (!string.IsNullOrWhiteSpace(colOp.DefaultSql))
        {
            fluentMethods.Add($".HasDefaultValueSql(\"{EscapeString(colOp.DefaultSql)}\")");
        }

        // Max length (if VARCHAR with length)
        var maxLength = ExtractMaxLength(colOp.GetSqlType());
        if (maxLength.HasValue)
        {
            fluentMethods.Add($".HasMaxLength({maxLength.Value})");
        }

        // Precision/Scale (if NUMERIC)
        var (precision, scale) = ExtractPrecisionScale(colOp);
        if (precision.HasValue)
        {
            if (scale.HasValue)
                fluentMethods.Add($".HasPrecision({precision.Value}, {scale.Value})");
            else
                fluentMethods.Add($".HasPrecision({precision.Value})");
        }

        // Unique constraint (marked via isUnique parameter)
        if (isUnique)
        {
            fluentMethods.Add(".IsUnique()");
        }

        foreach (var method in fluentMethods)
        {
            sb.AppendLine();
            sb.Append($"{indent}    {method}");
        }

        sb.Append(isLast ? "" : ",");
        sb.AppendLine();
    }

    private static void GenerateAlterTableOperations(
        StringBuilder sb,
        IReadOnlyList<MigrationOperation> operations,
        string indent)
    {
        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case MigrationOperationType.AddColumn:
                    var addColumnCall = GenerateAddColumnCall(op, indent);
                    sb.AppendLine(addColumnCall);
                    break;

                case MigrationOperationType.DropColumn:
                    sb.AppendLine(
                        $"{indent}migrationBuilder.DropColumn(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\");");
                    break;

                case MigrationOperationType.AlterColumnType:
                    sb.AppendLine(
                        $"{indent}migrationBuilder.AlterColumnType(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\", \"{op.GetSqlType()}\");");
                    break;

                case MigrationOperationType.AlterNullability:
                    sb.AppendLine(
                        $"{indent}migrationBuilder.AlterNullability(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\", {(op.IsNullable!.Value ? "true" : "false")});");
                    break;

                case MigrationOperationType.SetDefault:
                    sb.AppendLine(
                        $"{indent}migrationBuilder.SetDefault(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\", \"{EscapeString(op.DefaultSql!)}\");");
                    break;

                case MigrationOperationType.DropDefault:
                    sb.AppendLine(
                        $"{indent}migrationBuilder.DropDefault(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\");");
                    break;

                case MigrationOperationType.AddUnique:
                    sb.AppendLine(
                        $"{indent}migrationBuilder.AddUnique(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\");");
                    break;

                case MigrationOperationType.DropConstraint:
                    sb.AppendLine(
                        $"{indent}migrationBuilder.DropConstraint(\"{op.Schema}\", \"{op.Table}\", \"{op.ConstraintName}\");");
                    break;

                case MigrationOperationType.AddForeignKey:
                    var fkCall = GenerateAddForeignKeyCall(op, indent);
                    sb.AppendLine(fkCall);
                    break;
            }
        }
    }

    private static void GenerateOtherOperations(
        StringBuilder sb,
        IReadOnlyList<MigrationOperation> operations,
        string indent)
    {
        var createdSchemas = new HashSet<string>();

        foreach (var op in operations)
        {
            if (op.Type == MigrationOperationType.CreateSchema && createdSchemas.Add(op.Schema))
            {
                sb.AppendLine($"{indent}migrationBuilder.CreateSchema(\"{op.Schema}\");");
            }
        }
    }

    private static void GenerateReverseOperations(
        StringBuilder sb,
        IReadOnlyList<MigrationOperation> operations,
        IReadOnlyList<MigrationOperation> allCommittedOperations,
        string indent)
    {
        var reversedOps = operations.Reverse().ToList();
        var droppedTables = new HashSet<(string Schema, string Table)>();
        reversedOps.Where(op => op.Type == MigrationOperationType.CreateTable).ToList()
            .ForEach(t => droppedTables.Add((t.Schema, t.Table)));

        // Build a lookup of all table definitions from committed operations
        var tableDefinitions = BuildTableDefinitionsFromCommittedOperations(allCommittedOperations);

        foreach (var op in reversedOps)
        {
            switch (op.Type)
            {
                // case MigrationOperationType.CreateSchema:
                //     sb.AppendLine($"{indent}migrationBuilder.DropSchema(\"{op.Schema}\");");
                //     break;

                case MigrationOperationType.CreateTable:
                    sb.AppendLine($"{indent}migrationBuilder.DropTable(\"{op.Schema}\", \"{op.Table}\");");
                    break;

                case MigrationOperationType.DropTable:
                    // Try to recreate the table from committed operations
                    var tableKey = (op.Schema, op.Table);
                    if (tableDefinitions.TryGetValue(tableKey, out var tableDef))
                    {
                        // Recreate the table using fluent API
                        GenerateCreateTableFluent(sb, tableDef, indent);
                    }
                    else
                    {
                        sb.AppendLine(
                            $"{indent}// TODO: Recreate table \"{op.Schema}.{op.Table}\" - structure not available in committed migrations");
                    }

                    break;

                case MigrationOperationType.AddColumn:
                    if (!droppedTables.Contains((op.Schema, op.Table)))
                        sb.AppendLine(
                            $"{indent}migrationBuilder.DropColumn(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\");");
                    break;

                case MigrationOperationType.DropColumn:
                    if (!droppedTables.Contains((op.Schema, op.Table)))
                    {
                        // Try to find the column definition from committed operations
                        var columnKey = (op.Schema, op.Table);
                        if (tableDefinitions.TryGetValue(columnKey, out var tableDefForColumn))
                        {
                            var columnOp = tableDefForColumn.Operations.FirstOrDefault(o =>
                                o.Type == MigrationOperationType.AddColumn && o.Column == op.Column);

                            if (columnOp != null)
                            {
                                var reverseAddColumnCall = GenerateAddColumnCall(columnOp, indent);
                                sb.AppendLine(reverseAddColumnCall);
                            }
                            else
                            {
                                sb.AppendLine(
                                    $"{indent}// TODO: Recreate column \"{op.Schema}.{op.Table}.{op.Column}\" - column metadata not available");
                            }
                        }
                        else
                        {
                            sb.AppendLine(
                                $"{indent}// TODO: Recreate column \"{op.Schema}.{op.Table}.{op.Column}\" - column metadata not available");
                        }
                    }

                    break;

                case MigrationOperationType.AlterColumnType:
                    if (!droppedTables.Contains((op.Schema, op.Table)))
                    {
                        // Try to find the original column type from committed operations
                        var originalColumnOp =
                            FindOriginalColumnOperation(tableDefinitions, op.Schema, op.Table, op.Column!);
                        if (originalColumnOp != null && !string.IsNullOrWhiteSpace(originalColumnOp.GetSqlType()))
                        {
                            sb.AppendLine(
                                $"{indent}migrationBuilder.AlterColumnType(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\", \"{originalColumnOp.GetSqlType()}\");");
                        }
                        else
                        {
                            sb.AppendLine(
                                $"{indent}// TODO: Revert column type for \"{op.Schema}.{op.Table}.{op.Column}\" - old type not available");
                        }
                    }

                    break;

                case MigrationOperationType.AlterNullability:
                    if (!droppedTables.Contains((op.Schema, op.Table)))
                    {
                        // Try to find the original nullability from committed operations
                        var originalColumnOp =
                            FindOriginalColumnOperation(tableDefinitions, op.Schema, op.Table, op.Column!);
                        if (originalColumnOp != null && originalColumnOp.IsNullable.HasValue)
                        {
                            sb.AppendLine(
                                $"{indent}migrationBuilder.AlterNullability(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\", {(originalColumnOp.IsNullable.Value ? "true" : "false")});");
                        }
                        else
                        {
                            sb.AppendLine(
                                $"{indent}// TODO: Revert nullability for \"{op.Schema}.{op.Table}.{op.Column}\" - old nullability not available");
                        }
                    }

                    break;

                case MigrationOperationType.SetDefault:
                    if (!droppedTables.Contains((op.Schema, op.Table)))
                    {
                        // Try to find the original default from committed operations
                        var originalColumnOp =
                            FindOriginalColumnOperation(tableDefinitions, op.Schema, op.Table, op.Column!);
                        if (originalColumnOp != null && !string.IsNullOrWhiteSpace(originalColumnOp.DefaultSql))
                        {
                            sb.AppendLine(
                                $"{indent}migrationBuilder.SetDefault(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\", \"{EscapeString(originalColumnOp.DefaultSql)}\");");
                        }
                        else
                        {
                            sb.AppendLine(
                                $"{indent}migrationBuilder.DropDefault(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\");");
                        }
                    }

                    break;

                case MigrationOperationType.DropDefault:
                    if (!droppedTables.Contains((op.Schema, op.Table)) && !string.IsNullOrWhiteSpace(op.DefaultSql))
                    {
                        sb.AppendLine(
                            $"{indent}migrationBuilder.SetDefault(\"{op.Schema}\", \"{op.Table}\", \"{op.Column}\", \"{EscapeString(op.DefaultSql)}\");");
                    }
                    else if (!droppedTables.Contains((op.Schema, op.Table)))
                    {
                        sb.AppendLine(
                            $"{indent}// TODO: Restore default for \"{op.Schema}.{op.Table}.{op.Column}\" - default value not available");
                    }

                    break;

                case MigrationOperationType.AddUnique:
                    if (!droppedTables.Contains((op.Schema, op.Table)) && !string.IsNullOrWhiteSpace(op.ConstraintName))
                    {
                        sb.AppendLine(
                            $"{indent}migrationBuilder.DropConstraint(\"{op.Schema}\", \"{op.Table}\", \"{op.ConstraintName}\");");
                    }

                    break;

                case MigrationOperationType.AddForeignKey:
                    if (!droppedTables.Contains((op.Schema, op.Table)) && !string.IsNullOrWhiteSpace(op.ConstraintName))
                    {
                        sb.AppendLine(
                            $"{indent}migrationBuilder.DropConstraint(\"{op.Schema}\", \"{op.Table}\", \"{op.ConstraintName}\");");
                    }

                    break;

                case MigrationOperationType.DropConstraint:
                    if (!droppedTables.Contains((op.Schema, op.Table)))
                    {
                        sb.AppendLine(
                            $"{indent}// TODO: Recreate constraint \"{op.ConstraintName}\" on \"{op.Schema}.{op.Table}\" - constraint definition not available");
                    }

                    break;
            }
        }
    }

    private static List<TableOperationGroup> GroupOperationsByTable(IReadOnlyList<MigrationOperation> operations)
    {
        var groups = new List<TableOperationGroup>();
        var tableOps = new Dictionary<(string Schema, string Table), List<MigrationOperation>>();

        // Separate CreateSchema operations
        var schemaOps = operations.Where(op => op.Type == MigrationOperationType.CreateSchema).ToList();
        if (schemaOps.Count != 0)
        {
            groups.Add(new TableOperationGroup
            {
                Type = TableOperationType.Other,
                Operations = schemaOps
            });
        }

        // Group table operations
        foreach (var op in operations)
        {
            if (op.Type == MigrationOperationType.CreateSchema)
                continue;

            var key = (op.Schema, op.Table);
            if (!tableOps.ContainsKey(key))
                tableOps[key] = [];

            tableOps[key].Add(op);
        }

        // Create groups based on operation types
        foreach (var (key, ops) in tableOps)
        {
            var hasCreateTable = ops.Any(op => op.Type == MigrationOperationType.CreateTable);
            var hasDropTable = ops.Any(op => op.Type == MigrationOperationType.DropTable);

            if (hasCreateTable)
            {
                groups.Add(new TableOperationGroup
                {
                    Type = TableOperationType.CreateTable,
                    Schema = key.Schema,
                    Table = key.Table,
                    Operations = ops
                });
            }
            else if (hasDropTable)
            {
                groups.Add(new TableOperationGroup
                {
                    Type = TableOperationType.DropTable,
                    Schema = key.Schema,
                    Table = key.Table,
                    Operations = ops
                });
            }
            else
            {
                groups.Add(new TableOperationGroup
                {
                    Type = TableOperationType.AlterTable,
                    Schema = key.Schema,
                    Table = key.Table,
                    Operations = ops
                });
            }
        }

        return groups;
    }

    private static string InferClrType(string sqlType)
    {
        var normalized = sqlType.ToUpperInvariant();

        // Extract base type (remove length/precision)
        var baseType = normalized.Split('(')[0].Trim();

        return baseType switch
        {
            "INTEGER" or "INT" or "INT4" => "int",
            "BIGINT" or "INT8" => "long",
            "SMALLINT" or "INT2" => "short",
            "BOOLEAN" or "BOOL" => "bool",
            "NUMERIC" or "DECIMAL" => "decimal",
            "REAL" or "FLOAT4" => "float",
            "DOUBLE PRECISION" or "FLOAT8" => "double",
            "VARCHAR" or "TEXT" or "CHARACTER VARYING" or "CHAR" => "string",
            "TIMESTAMP" or "TIMESTAMP WITHOUT TIME ZONE" => "DateTime",
            "TIMESTAMPTZ" or "TIMESTAMP WITH TIME ZONE" => "DateTimeOffset",
            "UUID" => "Guid",
            "BYTEA" => "byte[]",
            "DATE" => "DateOnly",
            "TIME" => "TimeOnly",
            _ => "string"
        };
    }

    private static int? ExtractMaxLength(string? sqlType)
    {
        if (string.IsNullOrWhiteSpace(sqlType))
            return null;

        var match = Regex.Match(sqlType, @"(VAR)?CHAR\((\d+)\)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[2].Value, out var length))
            return length;

        return null;
    }

    private static (int? Precision, int? Scale) ExtractPrecisionScale(MigrationOperation op)
    {
        return (op.NumericPrecision, op.NumericScale);
    }

    private static string GenerateAddColumnCall(MigrationOperation op, string indent)
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
        return $"{indent}migrationBuilder.AddColumn({allParams});";
    }

    private static string GenerateAddForeignKeyCall(MigrationOperation op, string indent)
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

        return $"{indent}migrationBuilder.AddForeignKey({string.Join(", ", parts)});";
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", @"\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Finds the original column operation from committed operations.
    /// Used to retrieve the original state of a column before alterations.
    /// </summary>
    private static MigrationOperation? FindOriginalColumnOperation(
        Dictionary<(string Schema, string Table), TableOperationGroup> tableDefinitions,
        string schema,
        string table,
        string column)
    {
        var key = (schema, table);
        if (!tableDefinitions.TryGetValue(key, out var tableDef))
            return null;

        // Find the AddColumn operation for this column
        return tableDef.Operations.FirstOrDefault(op =>
            op.Type == MigrationOperationType.AddColumn && op.Column == column);
    }

    /// <summary>
    /// Builds table definitions from all committed operations, grouping operations by table.
    /// This allows us to reconstruct the full table structure for rollback operations.
    /// </summary>
    private static Dictionary<(string Schema, string Table), TableOperationGroup>
        BuildTableDefinitionsFromCommittedOperations(
            IReadOnlyList<MigrationOperation> allCommittedOperations)
    {
        var tableDefinitions = new Dictionary<(string Schema, string Table), TableOperationGroup>();

        // Group all operations by table
        var tableOps = new Dictionary<(string Schema, string Table), List<MigrationOperation>>();

        foreach (var op in allCommittedOperations)
        {
            // Skip schema-only operations
            if (op.Type == MigrationOperationType.CreateSchema)
                continue;

            var key = (op.Schema, op.Table);
            if (!tableOps.ContainsKey(key))
                tableOps[key] = [];

            tableOps[key].Add(op);
        }

        // Build table definitions
        foreach (var (key, ops) in tableOps)
        {
            var hasCreateTable = ops.Any(op => op.Type == MigrationOperationType.CreateTable);

            // Only include tables that have been created (not just altered)
            if (hasCreateTable)
            {
                tableDefinitions[key] = new TableOperationGroup
                {
                    Type = TableOperationType.CreateTable,
                    Schema = key.Schema,
                    Table = key.Table,
                    Operations = ops
                };
            }
        }

        return tableDefinitions;
    }

    private class TableOperationGroup
    {
        public required TableOperationType Type { get; init; }
        public string Schema { get; init; } = "";
        public string Table { get; init; } = "";
        public required IReadOnlyList<MigrationOperation> Operations { get; init; }
    }

    private enum TableOperationType
    {
        CreateTable,
        AlterTable,
        DropTable,
        Other
    }
}