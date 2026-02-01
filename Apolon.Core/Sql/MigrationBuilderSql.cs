using Apolon.Core.Attributes;
using Apolon.Core.Mapping;

namespace Apolon.Core.Sql;

internal static class MigrationBuilderSql
{
    public static string BuildCreateSchema(string schemaName)
    {
        return $"CREATE SCHEMA IF NOT EXISTS {schemaName};";
    }

    public static string BuildCreateTable(Type entityType)
    {
        var metadata = EntityMapper.GetMetadata(entityType);
        var lines = new List<string>
        {
            $"CREATE TABLE IF NOT EXISTS {metadata.Schema}.{metadata.TableName} ("
        };

        // Add columns
        foreach (var column in metadata.Columns)
        {
            var line = $"    {column.ColumnName} {column.DbType}";

            // Constraints
            if (column.ColumnName == metadata.PrimaryKey.ColumnName)
            {
                line += metadata.PrimaryKey.AutoIncrement
                    ? " PRIMARY KEY GENERATED ALWAYS AS IDENTITY"
                    : " PRIMARY KEY";
            }
            else
            {
                // DEFAULT must come before NOT NULL in PostgreSQL
                if (column.DefaultValue != null)
                {
                    var formatted = column.DefaultIsRawSql
                        ? column.DefaultValue.ToString()
                        : FormatValue(column.DefaultValue);
                    line += $" DEFAULT {formatted}";
                }

                if (!column.IsNullable) line += " NOT NULL";
                if (column.IsUnique) line += " UNIQUE";
            }

            lines.Add(line + ",");
        }

        // Add foreign keys
        foreach (var fk in metadata.ForeignKeys)
        {
            var refMetadata = EntityMapper.GetMetadata(fk.ReferencedTable);
            var line = $"    CONSTRAINT {metadata.TableName}_{fk.ColumnName}_fkey " +
                       $"FOREIGN KEY ({fk.ColumnName}) " +
                       $"REFERENCES {refMetadata.Schema}.{refMetadata.TableName}({fk.ReferencedColumn}) " +
                       $"ON DELETE {fk.OnDeleteBehavior.ToSql()}";
            lines.Add(line + ",");
        }

        // Remove trailing comma from last line
        if (lines.Count > 1)
        {
            lines[^1] = lines[^1].TrimEnd(',');
        }

        lines.Add(");");

        return string.Join("\n", lines);
    }

    public static string BuildDropTable(Type entityType, bool cascade = true)
    {
        var metadata = EntityMapper.GetMetadata(entityType);
        var cascadeSql = cascade ? " CASCADE" : "";
        return $"DROP TABLE IF EXISTS {metadata.Schema}.{metadata.TableName}{cascadeSql};";
    }

    public static string BuildTruncateTable(Type entityType)
    {
        var metadata = EntityMapper.GetMetadata(entityType);
        return $"TRUNCATE TABLE {metadata.Schema}.{metadata.TableName} RESTART IDENTITY CASCADE;";
    }

    public static string BuildCreateTableFromName(string schema, string table)
        => $"CREATE TABLE {schema}.{table} ();";

    public static string BuildDropTableFromName(string schema, string table, bool cascade = true)
    {
        var cascadeSql = cascade ? " CASCADE" : "";
        return $"DROP TABLE IF EXISTS {schema}.{table}{cascadeSql};";
    }

    public static string BuildAddColumn(
        string schema,
        string table,
        string column,
        string sqlType,
        bool isNullable,
        string? defaultSql,
        bool isPrimaryKey = false,
        bool isIdentity = false,
        string? identityGeneration = null)
    {
        // PRIMARY KEY and IDENTITY columns are implicitly NOT NULL, so we don't add it explicitly
        var nullSql = (isNullable || isPrimaryKey || isIdentity) ? "" : " NOT NULL";
        var defaultClause = string.IsNullOrWhiteSpace(defaultSql) ? "" : $" DEFAULT {defaultSql}";
        var pkClause = isPrimaryKey ? " PRIMARY KEY" : "";
        var identityClause = isIdentity ? $" GENERATED {NormalizeIdentity(identityGeneration)} AS IDENTITY" : "";
        return $"ALTER TABLE {schema}.{table} ADD COLUMN {column} {sqlType}{defaultClause}{nullSql}{pkClause}{identityClause};";
    }

    public static string BuildDropColumn(string schema, string table, string column)
        => $"ALTER TABLE {schema}.{table} DROP COLUMN IF EXISTS {column};";

    public static string BuildAlterColumnType(string schema, string table, string column, string sqlType)
        => $"ALTER TABLE {schema}.{table} ALTER COLUMN {column} TYPE {sqlType};";

    public static string BuildAlterNullability(string schema, string table, string column, bool isNullable)
        => isNullable
            ? $"ALTER TABLE {schema}.{table} ALTER COLUMN {column} DROP NOT NULL;"
            : $"ALTER TABLE {schema}.{table} ALTER COLUMN {column} SET NOT NULL;";

    public static string BuildSetDefault(string schema, string table, string column, string defaultSql)
        => $"ALTER TABLE {schema}.{table} ALTER COLUMN {column} SET DEFAULT {defaultSql};";

    public static string BuildDropDefault(string schema, string table, string column)
        => $"ALTER TABLE {schema}.{table} ALTER COLUMN {column} DROP DEFAULT;";

    public static string BuildAddUnique(string schema, string table, string column)
        => $"ALTER TABLE {schema}.{table} ADD CONSTRAINT {table}_{column}_key UNIQUE ({column});";

    public static string BuildDropConstraint(string schema, string table, string constraintName)
        => $"ALTER TABLE {schema}.{table} DROP CONSTRAINT IF EXISTS {constraintName};";

    public static string BuildAddForeignKey(
        string schema,
        string table,
        string column,
        string constraintName,
        string refSchema,
        string refTable,
        string refColumn,
        OnDeleteBehavior onDelete = OnDeleteBehavior.NoAction)
        => $"ALTER TABLE {schema}.{table} ADD CONSTRAINT {constraintName} " +
           $"FOREIGN KEY ({column}) REFERENCES {refSchema}.{refTable}({refColumn}) " +
           $"ON DELETE {onDelete.ToSql()};";

    private static string FormatValue(object value)
    {
        return value switch
        {
            string s => $"'{s}'",
            bool b => b ? "true" : "false",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            _ => value.ToString()
        } ?? throw new InvalidOperationException();
    }

    private static string NormalizeIdentity(string? identityGeneration)
    {
        return identityGeneration?.ToUpperInvariant() switch
        {
            "ALWAYS" => "ALWAYS",
            "BY DEFAULT" => "BY DEFAULT",
            "BYDEFAULT" => "BY DEFAULT",
            _ => "ALWAYS"
        };
    }
}
