using Apolon.Core.Mapping;

namespace Apolon.Core.SqlBuilders;

public static class MigrationBuilder
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
            $"CREATE TABLE {metadata.Schema}.{metadata.TableName} ("
        };

        // Add columns
        foreach (var column in metadata.Columns)
        {
            var line = $"    {column.ColumnName} {column.DbType}";

            // Constraints
            if (column.ColumnName == metadata.PrimaryKey.ColumnName)
            {
                line += metadata.PrimaryKey.AutoIncrement ? " PRIMARY KEY GENERATED ALWAYS AS IDENTITY" : " PRIMARY KEY";
            }
            else
            {
                if (!column.IsNullable) line += " NOT NULL";
                if (column.DefaultValue != null) line += $" DEFAULT {FormatValue(column.DefaultValue)}";
                if (column.IsUnique) line += " UNIQUE";
            }

            lines.Add(line + ",");
        }

        // Add foreign keys
        foreach (var fk in metadata.ForeignKeys)
        {
            var refMetadata = EntityMapper.GetMetadata(fk.ReferencedTable);
            var line = $"    CONSTRAINT fk_{metadata.TableName}_{fk.ColumnName} " +
                      $"FOREIGN KEY ({fk.ColumnName}) " +
                      $"REFERENCES {refMetadata.Schema}.{refMetadata.TableName}({fk.ReferencedColumn}) " +
                      $"ON DELETE {fk.OnDeleteBehavior}";
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
}