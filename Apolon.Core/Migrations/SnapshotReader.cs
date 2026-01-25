using Apolon.Core.Attributes;
using Apolon.Core.DataAccess;
using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations;

internal static class SnapshotReader
{
    private const string Sql = """
                               WITH fk AS (
                                   SELECT
                                       kcu.constraint_schema,
                                       kcu.constraint_name,
                                       kcu.table_schema,
                                       kcu.table_name,
                                       kcu.column_name,
                                       kcu.ordinal_position,
                                       kcu.position_in_unique_constraint
                                   FROM information_schema.key_column_usage kcu
                                   JOIN information_schema.table_constraints tco
                                     ON kcu.constraint_schema = tco.constraint_schema
                                    AND kcu.constraint_name = tco.constraint_name
                                    AND tco.constraint_type = 'FOREIGN KEY'
                               ),
                               pk AS (
                                   SELECT
                                       kcu.constraint_schema,
                                       kcu.constraint_name,
                                       kcu.table_schema,
                                       kcu.table_name,
                                       kcu.column_name
                                   FROM information_schema.key_column_usage kcu
                                   JOIN information_schema.table_constraints tco
                                     ON kcu.constraint_schema = tco.constraint_schema
                                    AND kcu.constraint_name = tco.constraint_name
                                    AND tco.constraint_type = 'PRIMARY KEY'
                               ),
                               uq AS (
                                   SELECT
                                       kcu.constraint_schema,
                                       kcu.constraint_name,
                                       kcu.table_schema,
                                       kcu.table_name,
                                       kcu.column_name
                                   FROM information_schema.key_column_usage kcu
                                   JOIN information_schema.table_constraints tco
                                     ON kcu.constraint_schema = tco.constraint_schema
                                    AND kcu.constraint_name = tco.constraint_name
                                    AND tco.constraint_type = 'UNIQUE'
                               )
                               SELECT 
                                   -- Identity of the column
                                   col.table_schema,
                                   col.table_name,
                                   col.ordinal_position,
                                   col.column_name,

                                   -- Type details (for precise diffing)
                                   col.data_type,
                                   col.udt_name,
                                   col.character_maximum_length,
                                   col.numeric_precision,
                                   col.numeric_scale,
                                   col.datetime_precision,

                                   -- Nullability + default
                                   col.is_nullable,
                                   col.column_default,

                                   -- Identity / generated columns (PostgreSQL exposes these in information_schema)
                                   col.is_identity,
                                   col.identity_generation,
                                   col.is_generated,
                                   col.generation_expression,

                                   -- PK / UNIQUE (column-level view)
                                   CASE WHEN pk.constraint_name IS NOT NULL THEN 'YES' ELSE 'NO' END AS is_primary_key,
                                   pk.constraint_name AS pk_constraint_name,

                                   CASE WHEN uq.constraint_name IS NOT NULL THEN 'YES' ELSE 'NO' END AS is_unique,
                                   uq.constraint_name AS unique_constraint_name,

                                   -- FK details
                                   CASE WHEN fk.constraint_name IS NOT NULL THEN 'YES' ELSE 'NO' END AS is_foreign_key,
                                   fk.constraint_name AS fk_constraint_name,
                                   rel.table_schema AS references_schema,
                                   rel.table_name   AS references_table,
                                   rel.column_name  AS references_column,
                                   rco.update_rule  AS fk_update_rule,
                                   rco.delete_rule  AS fk_delete_rule
                               FROM information_schema.columns col
                               LEFT JOIN fk
                                 ON col.table_schema = fk.table_schema
                                AND col.table_name = fk.table_name
                                AND col.column_name = fk.column_name
                               LEFT JOIN information_schema.referential_constraints rco
                                 ON rco.constraint_name = fk.constraint_name
                                AND rco.constraint_schema = fk.constraint_schema
                               LEFT JOIN information_schema.key_column_usage rel
                                 ON rco.unique_constraint_name = rel.constraint_name
                                AND rco.unique_constraint_schema = rel.constraint_schema
                                AND rel.ordinal_position = fk.position_in_unique_constraint

                               LEFT JOIN pk
                                 ON col.table_schema = pk.table_schema
                                AND col.table_name = pk.table_name
                                AND col.column_name = pk.column_name
                               LEFT JOIN uq
                                 ON col.table_schema = uq.table_schema
                                AND col.table_name = uq.table_name
                                AND col.column_name = uq.column_name

                               WHERE col.table_schema NOT IN ('information_schema', 'pg_catalog', 'pg_toast')
                                 AND col.table_schema NOT LIKE 'pg_temp%'
                                 AND col.table_schema NOT LIKE 'pg_toast_temp%'
                               ORDER BY col.table_schema, col.table_name, col.ordinal_position;
                               """;

    public static async Task<SchemaSnapshot> ReadAsync(
        IDbConnection db,
        CancellationToken ct = default)
    {
        // If you want a consistent snapshot while schema may change, uncomment these:
        // db.BeginTransaction();

        try
        {
            // await db.OpenConnectionAsync();

            await using var cmd = db.CreateCommand(Sql);

            var tableMap = new Dictionary<(string Schema, string Table), List<ColumnSnapshot>>();

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var iSchema = reader.GetOrdinal("table_schema");
            var iTable = reader.GetOrdinal("table_name");
            var iColumn = reader.GetOrdinal("column_name");

            var iDataType = reader.GetOrdinal("data_type");
            var iUdtName = reader.GetOrdinal("udt_name");
            var iCharMaxLen = reader.GetOrdinal("character_maximum_length");
            var iNumPrecision = reader.GetOrdinal("numeric_precision");
            var iNumScale = reader.GetOrdinal("numeric_scale");
            var iDateTimePrecision = reader.GetOrdinal("datetime_precision");

            var iNullable = reader.GetOrdinal("is_nullable");
            var iDefault = reader.GetOrdinal("column_default");

            var iIsIdentity = reader.GetOrdinal("is_identity");
            var iIdentityGeneration = reader.GetOrdinal("identity_generation");
            var iIsGenerated = reader.GetOrdinal("is_generated");
            var iGenerationExpression = reader.GetOrdinal("generation_expression");

            var iIsPk = reader.GetOrdinal("is_primary_key");
            var iPkName = reader.GetOrdinal("pk_constraint_name");

            var iIsUnique = reader.GetOrdinal("is_unique");
            var iUniqueName = reader.GetOrdinal("unique_constraint_name");

            var iIsFk = reader.GetOrdinal("is_foreign_key");
            var iFkName = reader.GetOrdinal("fk_constraint_name");
            var iRefSchema = reader.GetOrdinal("references_schema");
            var iRefTable = reader.GetOrdinal("references_table");
            var iRefCol = reader.GetOrdinal("references_column");
            var iFkUpdate = reader.GetOrdinal("fk_update_rule");
            var iFkDelete = reader.GetOrdinal("fk_delete_rule");

            while (await reader.ReadAsync(ct))
            {
                // Raw read
                var schemaRaw = reader.GetString(iSchema);
                var tableRaw = reader.GetString(iTable);
                var columnRaw = reader.GetString(iColumn);

                var isNullable = string.Equals(reader.GetString(iNullable), "YES", StringComparison.OrdinalIgnoreCase);

                var isPk = string.Equals(reader.GetString(iIsPk), "YES", StringComparison.OrdinalIgnoreCase);
                var isUnique = string.Equals(reader.GetString(iIsUnique), "YES", StringComparison.OrdinalIgnoreCase);
                var isForeignKey = string.Equals(reader.GetString(iIsFk), "YES", StringComparison.OrdinalIgnoreCase);

                var dataTypeRaw = reader.GetString(iDataType);
                var udtNameRaw = reader.GetString(iUdtName);

                int? charMaxLen = reader.IsDBNull(iCharMaxLen) ? null : reader.GetInt32(iCharMaxLen);
                int? numericPrecision = reader.IsDBNull(iNumPrecision)
                    ? null
                    : Convert.ToInt32(reader.GetValue(iNumPrecision));
                int? numericScale = reader.IsDBNull(iNumScale) ? null : Convert.ToInt32(reader.GetValue(iNumScale));
                int? dateTimePrecision = reader.IsDBNull(iDateTimePrecision)
                    ? null
                    : Convert.ToInt32(reader.GetValue(iDateTimePrecision));

                var defaultRaw = reader.IsDBNull(iDefault) ? null : reader.GetString(iDefault);

                var isIdentity = string.Equals(reader.GetString(iIsIdentity), "YES",
                    StringComparison.OrdinalIgnoreCase);
                var identityGenerationRaw =
                    reader.IsDBNull(iIdentityGeneration) ? null : reader.GetString(iIdentityGeneration);

                var isGeneratedRaw = reader.GetString(iIsGenerated); // "ALWAYS" or "NEVER"
                var isGenerated = string.Equals(isGeneratedRaw, "ALWAYS", StringComparison.OrdinalIgnoreCase);
                var generationExpressionRaw = reader.IsDBNull(iGenerationExpression)
                    ? null
                    : reader.GetString(iGenerationExpression);

                var pkNameRaw = reader.IsDBNull(iPkName) ? null : reader.GetString(iPkName);
                var uniqueNameRaw = reader.IsDBNull(iUniqueName) ? null : reader.GetString(iUniqueName);

                var fkNameRaw = reader.IsDBNull(iFkName) ? null : reader.GetString(iFkName);
                var referencesSchemaRaw = reader.IsDBNull(iRefSchema) ? null : reader.GetString(iRefSchema);
                var referencesTableRaw = reader.IsDBNull(iRefTable) ? null : reader.GetString(iRefTable);
                var referencesColumnRaw = reader.IsDBNull(iRefCol) ? null : reader.GetString(iRefCol);
                var fkUpdateRuleRaw = reader.IsDBNull(iFkUpdate) ? null : reader.GetString(iFkUpdate);
                var fkDeleteRuleRaw = reader.IsDBNull(iFkDelete) ? null : reader.GetString(iFkDelete);

                // Normalization (stable diffing)
                var schema = SnapshotNormalization.NormalizeIdentifier(schemaRaw);
                var table = SnapshotNormalization.NormalizeIdentifier(tableRaw);
                var columnName = SnapshotNormalization.NormalizeIdentifier(columnRaw);

                var dataType = SnapshotNormalization.NormalizeDataType(dataTypeRaw);
                var udtName = SnapshotNormalization.NormalizeIdentifier(udtNameRaw);

                var columnDefault = SnapshotNormalization.NormalizeDefault(defaultRaw);

                var identityGeneration = identityGenerationRaw is null
                    ? null
                    : SnapshotNormalization.NormalizeIdentifier(identityGenerationRaw);

                var pkConstraintName = pkNameRaw is null ? null : SnapshotNormalization.NormalizeIdentifier(pkNameRaw);
                var uniqueConstraintName = uniqueNameRaw is null
                    ? null
                    : SnapshotNormalization.NormalizeIdentifier(uniqueNameRaw);

                var fkConstraintName = fkNameRaw is null ? null : SnapshotNormalization.NormalizeIdentifier(fkNameRaw);
                var referencesSchema = referencesSchemaRaw is null
                    ? null
                    : SnapshotNormalization.NormalizeIdentifier(referencesSchemaRaw);
                var referencesTable = referencesTableRaw is null
                    ? null
                    : SnapshotNormalization.NormalizeIdentifier(referencesTableRaw);
                var referencesColumn = referencesColumnRaw is null
                    ? null
                    : SnapshotNormalization.NormalizeIdentifier(referencesColumnRaw);

                var fkUpdateRule = fkUpdateRuleRaw is null
                    ? OnDeleteBehavior.NoAction.ToSql()
                    : SnapshotNormalization.CollapseWhitespace(fkUpdateRuleRaw).ToUpperInvariant();
                var fkDeleteRule = fkDeleteRuleRaw is null
                    ? null
                    : SnapshotNormalization.CollapseWhitespace(fkDeleteRuleRaw).ToUpperInvariant();

                var col = new ColumnSnapshot(
                    ColumnName: columnName,
                    DataType: dataType,
                    IsNullable: isNullable,
                    ColumnDefault: columnDefault,
                    IsForeignKey: isForeignKey,
                    FkConstraintName: fkConstraintName,
                    ReferencesSchema: referencesSchema,
                    ReferencesTable: referencesTable,
                    ReferencesColumn: referencesColumn,
                    FkUpdateRule: fkUpdateRule,
                    FkDeleteRule: fkDeleteRule,
                    IsPrimaryKey: isPk,
                    PkConstraintName: pkConstraintName,
                    IsUnique: isUnique,
                    UniqueConstraintName: uniqueConstraintName,
                    UdtName: udtName,
                    CharacterMaximumLength: charMaxLen,
                    NumericPrecision: numericPrecision,
                    NumericScale: numericScale,
                    DateTimePrecision: dateTimePrecision,
                    IsIdentity: isIdentity,
                    IdentityGeneration: identityGeneration,
                    IsGenerated: isGenerated,
                    GenerationExpression: generationExpressionRaw
                );

                var key = (schema, table);
                if (!tableMap.TryGetValue(key, out var cols))
                {
                    cols = new List<ColumnSnapshot>();
                    tableMap[key] = cols;
                }

                cols.Add(col);
            }

            // db.CommitTransaction();

            var tables = tableMap
                .OrderBy(x => x.Key.Schema)
                .ThenBy(x => x.Key.Table)
                .Select(x => new TableSnapshot(x.Key.Schema, x.Key.Table, x.Value))
                .ToList();

            return new SchemaSnapshot(tables);
        }
        catch
        {
            // db.RollbackTransaction();
            throw;
        }
        finally
        {
            // db.CloseConnection();
        }
    }
}