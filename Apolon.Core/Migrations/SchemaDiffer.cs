using Apolon.Core.Attributes;
using Apolon.Core.Mapping;
using Apolon.Core.Mapping.Models;
using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations;

internal enum MigrationOpKind
{
    CreateSchema,
    CreateTable,
    AddColumn,
    AlterColumnType,
    AlterNullability,
    SetDefault,
    DropDefault,
    AddUnique,
    DropConstraint,
    AddForeignKey
}

internal sealed record MigrationOp(
    MigrationOpKind Kind,
    string Schema,
    string Table,
    string? Column = null,
    string? SqlType = null,
    bool? IsNullable = null,
    string? DefaultSql = null,
    string? ConstraintName = null,
    string? RefSchema = null,
    string? RefTable = null,
    string? RefColumn = null,
    string? OnDeleteRule = null
);

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
                    fkConstraintName = SnapshotNormalization.NormalizeIdentifier($"fk_{m.TableName}_{fk.ColumnName}");
                    onDelete = fk.OnDeleteBehavior.ToSql();
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

internal static class SchemaDiffer
{
    public static IReadOnlyList<MigrationOp> Diff(SchemaSnapshot expected, SchemaSnapshot actual)
    {
        var ops = new List<MigrationOp>();

        var actualTables = actual.Tables.ToDictionary(t => (t.Schema, t.Table));
        var expectedTables = expected.Tables.ToDictionary(t => (t.Schema, t.Table));

        // 1) Create schemas / tables that are missing
        foreach (var (key, expTable) in expectedTables)
        {
            if (!actualTables.ContainsKey(key))
            {
                ops.Add(new MigrationOp(MigrationOpKind.CreateSchema, expTable.Schema, expTable.Table));
                ops.Add(new MigrationOp(MigrationOpKind.CreateTable, expTable.Schema, expTable.Table));
            }
        }

        // 2) For existing tables: columns diff
        foreach (var (key, expTable) in expectedTables)
        {
            if (!actualTables.TryGetValue(key, out var actTable))
                continue;

            var actCols = actTable.Columns.ToDictionary(c => c.ColumnName);
            var expCols = expTable.Columns.ToDictionary(c => c.ColumnName);

            // Add missing columns
            foreach (var (colName, expCol) in expCols)
            {
                if (!actCols.ContainsKey(colName))
                {
                    ops.Add(new MigrationOp(
                        Kind: MigrationOpKind.AddColumn,
                        Schema: expTable.Schema,
                        Table: expTable.Table,
                        Column: colName,
                        SqlType: expCol.DataType,
                        IsNullable: expCol.IsNullable,
                        DefaultSql: expCol.ColumnDefault
                    ));

                    if (expCol.IsUnique)
                    {
                        ops.Add(new MigrationOp(
                            Kind: MigrationOpKind.AddUnique,
                            Schema: expTable.Schema,
                            Table: expTable.Table,
                            Column: colName
                        ));
                    }

                    if (expCol.IsForeignKey)
                    {
                        ops.Add(new MigrationOp(
                            Kind: MigrationOpKind.AddForeignKey,
                            Schema: expTable.Schema,
                            Table: expTable.Table,
                            Column: colName,
                            ConstraintName: expCol.FkConstraintName,
                            RefSchema: expCol.ReferencesSchema,
                            RefTable: expCol.ReferencesTable,
                            RefColumn: expCol.ReferencesColumn,
                            OnDeleteRule: expCol.FkDeleteRule // may be null model-side; builder can default
                        ));
                    }
                }
            }

            // Alter existing columns (type/nullability/default)
            foreach (var (colName, expCol) in expCols)
            {
                if (!actCols.TryGetValue(colName, out var actCol))
                    continue;

                if (!string.Equals(expCol.DataType, actCol.DataType, StringComparison.Ordinal))
                {
                    ops.Add(new MigrationOp(
                        MigrationOpKind.AlterColumnType,
                        expTable.Schema,
                        expTable.Table,
                        Column: colName,
                        SqlType: expCol.DataType
                    ));
                }

                if (expCol.IsNullable != actCol.IsNullable)
                {
                    ops.Add(new MigrationOp(
                        MigrationOpKind.AlterNullability,
                        expTable.Schema,
                        expTable.Table,
                        Column: colName,
                        IsNullable: expCol.IsNullable
                    ));
                }

                // Default compare (normalized strings)
                if (!string.Equals(expCol.ColumnDefault, actCol.ColumnDefault, StringComparison.Ordinal))
                {
                    if (expCol.ColumnDefault is null)
                    {
                        ops.Add(new MigrationOp(
                            MigrationOpKind.DropDefault,
                            expTable.Schema,
                            expTable.Table,
                            Column: colName
                        ));
                    }
                    else
                    {
                        ops.Add(new MigrationOp(
                            MigrationOpKind.SetDefault,
                            expTable.Schema,
                            expTable.Table,
                            Column: colName,
                            DefaultSql: expCol.ColumnDefault
                        ));
                    }
                }
            }
        }

        // Important ordering note:
        // - Create schemas/tables first
        // - Add/alter columns next
        // - Constraints (unique/FK) last
        return ops;
    }
}