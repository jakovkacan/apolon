using Apolon.Core.Attributes;
using Apolon.Core.Mapping.Models;
using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Mapping.Converters;

/// <summary>
/// Converts entity metadata to normalized schema snapshots for migration diffing.
/// Centralizes all conversion logic between metadata and snapshot models.
/// </summary>
internal static class MetadataToSnapshotConverter
{
    /// <summary>
    /// Converts an entity metadata object to a table snapshot.
    /// </summary>
    public static TableSnapshot ToSnapshot(EntityMetadata metadata)
    {
        var schema = SnapshotNormalization.NormalizeIdentifier(metadata.Schema);
        var table = SnapshotNormalization.NormalizeIdentifier(metadata.TableName);

        var columns = metadata.Columns
            .Select(c => ToColumnSnapshot(c, metadata))
            .ToList();

        return new TableSnapshot(schema, table, columns);
    }

    /// <summary>
    /// Converts a property metadata object to a column snapshot.
    /// </summary>
    internal static ColumnSnapshot ToColumnSnapshot(PropertyMetadata prop, EntityMetadata entityContext)
    {
        var colName = SnapshotNormalization.NormalizeIdentifier(prop.ColumnName);

        var (characterMaximumLength, numericPrecision, numericScale)
            = SnapshotNormalization.ExtractDataTypeDetails(prop.DbType);

        // DbType comes from attributes/TypeMapper, so normalize to match DB snapshot normalization.
        var normalizedType = SnapshotNormalization.NormalizeDataType(prop.DbType);

        // Defaults: your model may store object values; for diffing we only compare SQL text.
        // If DefaultIsRawSql, we use it verbatim; otherwise, map basic CLR values to SQL.
        var defaultSql = prop.DefaultValue is null
            ? null
            : prop.DefaultIsRawSql
                ? SnapshotNormalization.NormalizeDefault(prop.DefaultValue.ToString())
                : SnapshotNormalization.NormalizeDefault(FormatDefaultValueAsSql(prop.DefaultValue));

        // IsPrimaryKey / IsIdentity will be inferred from PrimaryKeyMetadata
        var isPk = string.Equals(colName, SnapshotNormalization.NormalizeIdentifier(entityContext.PrimaryKey.ColumnName),
            StringComparison.Ordinal);
        var isIdentity = isPk && entityContext.PrimaryKey.AutoIncrement;

        // Unique is per column in your model
        var isUnique = prop.IsUnique;

        // FK: per column in your model
        var fk = entityContext.ForeignKeys.FirstOrDefault(x =>
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
            fkConstraintName = SnapshotNormalization.NormalizeIdentifier($"{entityContext.TableName}_{fk.ColumnName}_fkey");
            onDelete = fk.OnDeleteBehavior.ToSql();
        }

        return new ColumnSnapshot(
            ColumnName: colName,
            DataType: normalizedType,
            UdtName: normalizedType,
            CharacterMaximumLength: characterMaximumLength,
            NumericPrecision: numericPrecision,
            NumericScale: numericScale,
            DateTimePrecision: normalizedType == "timestamp" ? 6 : null,
            IsNullable: prop.IsNullable,
            ColumnDefault: defaultSql,
            IsIdentity: isIdentity,
            IdentityGeneration: isIdentity ? "always" : null,
            IsGenerated: false,
            GenerationExpression: null,
            IsPrimaryKey: isPk,
            PkConstraintName: isPk ? SnapshotNormalization.NormalizeIdentifier($"{entityContext.TableName}_pkey") : null,
            IsUnique: isUnique,
            UniqueConstraintName: isUnique
                ? SnapshotNormalization.NormalizeIdentifier($"{entityContext.TableName}_{prop.ColumnName}_key")
                : null,
            IsForeignKey: fk is not null,
            FkConstraintName: fkConstraintName,
            ReferencesSchema: refSchema,
            ReferencesTable: refTable,
            ReferencesColumn: refColumn,
            FkUpdateRule: OnDeleteBehavior.NoAction.ToSql(),
            FkDeleteRule: onDelete
        );
    }

    /// <summary>
    /// Formats a CLR default value as SQL literal text.
    /// </summary>
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
