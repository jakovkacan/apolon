using Apolon.Core.Attributes;
using Apolon.Core.Mapping;
using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations;

internal static class ModelSnapshotBuilder
{
    public static SchemaSnapshot BuildFromModel(params Type[] entityTypes)
    {
        var tables = new List<TableSnapshot>();

        foreach (var entityType in entityTypes)
        {
            var m = EntityMapper.GetMetadata(entityType);

            var schema = SnapshotNormalization.NormalizeIdentifier(m.Schema);
            var table = SnapshotNormalization.NormalizeIdentifier(m.TableName);

            var cols = new List<ColumnSnapshot>();

            foreach (var c in m.Columns)
            {
                var colName = SnapshotNormalization.NormalizeIdentifier(c.ColumnName);

                var (characterMaximumLength, numericPrecision, numericScale)
                    = SnapshotNormalization.ExtractDataTypeDetails(c.DbType);

                // DbType comes from attributes/TypeMapper, so normalize to match DB snapshot normalization.
                var normalizedType = SnapshotNormalization.NormalizeDataType(c.DbType);

                // Defaults: your model may store object values; for diffing we only compare SQL text.
                // If DefaultIsRawSql, we use it verbatim; otherwise, map basic CLR values to SQL.
                var defaultSql = c.DefaultValue is null
                    ? null
                    : c.DefaultIsRawSql
                        ? SnapshotNormalization.NormalizeDefault(c.DefaultValue.ToString())
                        : SnapshotNormalization.NormalizeDefault(FormatDefaultValueAsSql(c.DefaultValue));

                // IsPrimaryKey / IsIdentity will be inferred from PrimaryKeyMetadata
                var isPk = string.Equals(colName, SnapshotNormalization.NormalizeIdentifier(m.PrimaryKey.ColumnName),
                    StringComparison.Ordinal);
                var isIdentity = isPk && m.PrimaryKey.AutoIncrement;

                // Unique is per column in your model
                var isUnique = c.IsUnique;

                // FK: per column in your model
                var fk = m.ForeignKeys.FirstOrDefault(x =>
                    SnapshotNormalization.NormalizeIdentifier(x.ColumnName) == colName);

                string? refSchema = null;
                string? refTable = null;
                string? refColumn = null;
                string? fkConstraintName = null;
                string? onDelete = null;

                if (fk is not null)
                {
                    var refMeta = EntityMapper.GetMetadata(fk.ReferencedTable);
                    refSchema = SnapshotNormalization.NormalizeIdentifier(refMeta.Schema);
                    refTable = SnapshotNormalization.NormalizeIdentifier(refMeta.TableName);
                    refColumn = SnapshotNormalization.NormalizeIdentifier(fk.ReferencedColumn);

                    // Must match your naming convention in MigrationBuilder.BuildCreateTable
                    fkConstraintName = SnapshotNormalization.NormalizeIdentifier($"{m.TableName}_{fk.ColumnName}_fkey");                    onDelete = fk.OnDeleteBehavior.ToSql();
                }

                cols.Add(new ColumnSnapshot(
                    ColumnName: colName,
                    DataType: normalizedType,
                    UdtName: normalizedType,
                    CharacterMaximumLength: characterMaximumLength,
                    NumericPrecision: numericPrecision,
                    NumericScale: numericScale,
                    DateTimePrecision: normalizedType == "timestamp" ? 6 : null,
                    IsNullable: c.IsNullable,
                    ColumnDefault: defaultSql,
                    IsIdentity: isIdentity,
                    IdentityGeneration: isIdentity ? "always" : null,
                    IsGenerated: false,
                    GenerationExpression: null,
                    IsPrimaryKey: isPk,
                    PkConstraintName: isPk ? SnapshotNormalization.NormalizeIdentifier($"{m.TableName}_pkey") : null,
                    IsUnique: isUnique,
                    UniqueConstraintName: isUnique
                        ? SnapshotNormalization.NormalizeIdentifier($"{m.TableName}_{c.ColumnName}_key")
                        : null,
                    IsForeignKey: fk is not null,
                    FkConstraintName: fkConstraintName,
                    ReferencesSchema: refSchema,
                    ReferencesTable: refTable,
                    ReferencesColumn: refColumn,
                    FkUpdateRule: OnDeleteBehavior.NoAction.ToSql(),
                    FkDeleteRule: onDelete
                ));
            }

            tables.Add(new TableSnapshot(schema, table, cols));
        }

        return new SchemaSnapshot(tables);
    }

    private static string FormatDefaultValueAsSql(object value) => value switch
    {
        string s => $"'{s.Replace("'", "''")}'",
        bool b => b ? "true" : "false",
        DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
        DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss zzz}'",
        Guid g => $"'{g}'",
        _ => value.ToString() ?? throw new InvalidOperationException()
    };
}