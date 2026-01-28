using System.Reflection;
using Apolon.Core.Migrations.Builders;
using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations;

/// <summary>
/// Extension methods for MigrationBuilder providing EntityFramework-like fluent API.
/// </summary>
public static class MigrationBuilderExtensions
{
    /// <summary>
    /// Creates a table using fluent API syntax similar to EntityFramework.
    /// </summary>
    /// <typeparam name="TColumns">Anonymous type representing the columns structure.</typeparam>
    /// <param name="migrationBuilder">The migration builder.</param>
    /// <param name="name">The table name.</param>
    /// <param name="columns">Lambda defining the columns (e.g., table => new { Id = table.Column&lt;int&gt;(), ... }).</param>
    /// <param name="schema">The schema name (defaults to "public").</param>
    /// <param name="constraints">Lambda defining table constraints (e.g., PrimaryKey, ForeignKey).</param>
    /// <param name="comment">Optional table comment.</param>
    /// <returns>The table builder for additional configuration.</returns>
    public static CreateTableBuilder<TColumns> CreateTable<TColumns>(
        this MigrationBuilder migrationBuilder,
        string name,
        Func<ColumnsBuilder, TColumns> columns,
        string schema = "public",
        Action<CreateTableBuilder<TColumns>>? constraints = null,
        string? comment = null)
    {
        // 1. Execute the columns lambda to get column builders
        var columnsBuilder = new ColumnsBuilder();
        var columnsObject = columns(columnsBuilder);

        // 2. Extract column definitions using reflection
        var columnDefinitions = ExtractColumnDefinitions(columnsObject);

        // 3. Create initial table definition
        var tableDefinition = new TableDefinition
        {
            Schema = schema,
            Name = name,
            Columns = columnDefinitions,
            Comment = comment
        };

        // 4. Create table builder for constraints
        var tableBuilder = new CreateTableBuilder<TColumns>(tableDefinition);

        // 5. Apply constraints if provided
        constraints?.Invoke(tableBuilder);

        // 6. Build final table definition
        var finalTableDefinition = tableBuilder.Build();

        // 7. Convert to MigrationOperations (backward compatibility)
        ConvertToMigrationOperations(migrationBuilder, finalTableDefinition);

        return tableBuilder;
    }

    /// <summary>
    /// Extracts column definitions from the anonymous type returned by the columns lambda.
    /// </summary>
    private static List<ColumnDefinition> ExtractColumnDefinitions(object columnsObject)
    {
        var columnDefinitions = new List<ColumnDefinition>();

        if (columnsObject == null)
            return columnDefinitions;

        var type = columnsObject.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var value = prop.GetValue(columnsObject);

            // The value should be a ColumnBuilder<T>
            if (value == null)
                continue;

            var builderType = value.GetType();
            if (!builderType.IsGenericType || builderType.GetGenericTypeDefinition() != typeof(ColumnBuilder<>))
                continue;

            // Call internal Build method
            var buildMethod = builderType.GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Instance);
            if (buildMethod == null)
                continue;

            var columnDef = (ColumnDefinition?)buildMethod.Invoke(value, [prop.Name]);
            if (columnDef != null)
            {
                columnDefinitions.Add(columnDef);
            }
        }

        return columnDefinitions;
    }

    /// <summary>
    /// Converts a TableDefinition to legacy MigrationOperations for backward compatibility.
    /// </summary>
    private static void ConvertToMigrationOperations(
        MigrationBuilder migrationBuilder,
        TableDefinition tableDefinition)
    {
        // Create schema
        migrationBuilder.CreateSchema(tableDefinition.Schema);

        // Create table
        migrationBuilder.CreateTable(tableDefinition.Schema, tableDefinition.Name);

        // Add columns (mark PK columns if defined in constraints)
        var pkColumns = tableDefinition.PrimaryKey?.Columns.ToHashSet() ?? [];

        foreach (var column in tableDefinition.Columns)
        {
            var sqlType = BuildSqlType(column);
            var isPk = column.IsPrimaryKey || pkColumns.Contains(column.Name);

            migrationBuilder.AddColumn(
                schema: tableDefinition.Schema,
                table: tableDefinition.Name,
                column: column.Name,
                sqlType: sqlType,
                isNullable: column.IsNullable,
                defaultSql: column.DefaultValueSql ?? ConvertDefaultValue(column.DefaultValue),
                isPrimaryKey: isPk,
                isIdentity: column.IsIdentity,
                identityGeneration: column.IdentityGeneration
            );

            // Add unique constraint if column is unique (but not PK)
            if (column.IsUnique && !isPk)
            {
                migrationBuilder.AddUnique(tableDefinition.Schema, tableDefinition.Name, column.Name);
            }
        }

        // Add foreign keys
        foreach (var fk in tableDefinition.ForeignKeys)
        {
            if (fk.Columns.Count == 1 && fk.PrincipalColumns.Count == 1)
            {
                migrationBuilder.AddForeignKey(
                    schema: tableDefinition.Schema,
                    table: tableDefinition.Name,
                    column: fk.Columns[0],
                    constraintName: fk.Name,
                    refSchema: fk.PrincipalSchema ?? "public",
                    refTable: fk.PrincipalTable,
                    refColumn: fk.PrincipalColumns[0],
                    onDeleteRule: fk.OnDelete
                );
            }
        }
    }

    /// <summary>
    /// Builds the SQL type string from a column definition.
    /// </summary>
    private static string BuildSqlType(ColumnDefinition column)
    {
        // If explicit SQL type is provided, use it
        if (!string.IsNullOrWhiteSpace(column.SqlType))
            return column.SqlType;

        // Infer from CLR type
        var clrType = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;
        var baseType = InferSqlType(clrType);

        // Apply length/precision modifiers
        if (column.MaxLength.HasValue && IsLengthType(baseType))
            return $"{baseType}({column.MaxLength.Value})";

        if (column.Precision.HasValue && IsNumericType(baseType))
        {
            return column.Scale.HasValue
                ? $"{baseType}({column.Precision.Value},{column.Scale.Value})"
                : $"{baseType}({column.Precision.Value})";
        }

        return baseType;
    }

    /// <summary>
    /// Infers SQL type from CLR type.
    /// </summary>
    private static string InferSqlType(Type clrType)
    {
        return Type.GetTypeCode(clrType) switch
        {
            TypeCode.Boolean => "BOOLEAN",
            TypeCode.Byte => "SMALLINT",
            TypeCode.Int16 => "SMALLINT",
            TypeCode.Int32 => "INTEGER",
            TypeCode.Int64 => "BIGINT",
            TypeCode.Single => "REAL",
            TypeCode.Double => "DOUBLE PRECISION",
            TypeCode.Decimal => "NUMERIC",
            TypeCode.String => "TEXT",
            TypeCode.DateTime => "TIMESTAMP",
            _ => clrType == typeof(Guid) ? "UUID" :
                 clrType == typeof(byte[]) ? "BYTEA" :
                 clrType == typeof(DateTimeOffset) ? "TIMESTAMPTZ" :
                 clrType == typeof(TimeSpan) ? "INTERVAL" :
                 "TEXT"
        };
    }

    /// <summary>
    /// Checks if the type supports length modifier.
    /// </summary>
    private static bool IsLengthType(string baseType)
    {
        var normalized = baseType.ToUpperInvariant();
        return normalized is "VARCHAR" or "CHAR" or "CHARACTER" or "CHARACTER VARYING"
            or "VARBIT" or "BIT VARYING" or "BIT";
    }

    /// <summary>
    /// Checks if the type supports numeric precision/scale.
    /// </summary>
    private static bool IsNumericType(string baseType)
    {
        var normalized = baseType.ToUpperInvariant();
        return normalized is "NUMERIC" or "DECIMAL";
    }

    /// <summary>
    /// Converts a CLR default value to SQL expression.
    /// </summary>
    private static string? ConvertDefaultValue(object? value)
    {
        if (value == null)
            return null;

        return value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "true" : "false",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss}'",
            Guid g => $"'{g}'",
            _ => value.ToString()
        };
    }
}
