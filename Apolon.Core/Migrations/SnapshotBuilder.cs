using Apolon.Core.Attributes;
using Apolon.Core.Mapping;
using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations;

internal static class SnapshotBuilder
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
                    fkConstraintName = SnapshotNormalization.NormalizeIdentifier($"{m.TableName}_{fk.ColumnName}_fkey");
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

    public static SchemaSnapshot ApplyMigrations(SchemaSnapshot db, IReadOnlyList<MigrationOperation> operations)
    {
        var tables = db.Tables.ToList();
        var newTables = operations.Where(op => op.Type == MigrationOperationType.CreateTable)
            .Select(t => new TableSnapshot(t.Schema, t.Table, [])).ToList();
        tables.AddRange(newTables);

        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case MigrationOperationType.CreateSchema:
                    // Schema creation doesn't affect the snapshot structure
                    break;

                case MigrationOperationType.CreateTable:
                    // tables.Add(new TableSnapshot(op.Schema, op.Table, []));
                    break;

                case MigrationOperationType.DropTable:
                    tables.RemoveAll(t => t.Schema == op.Schema && t.Name == op.Table);
                    break;

                case MigrationOperationType.AddColumn:
                    var tableToAddColumn = tables.FirstOrDefault(t => t.Schema == op.Schema && t.Name == op.Table);
                    if (tableToAddColumn != null)
                    {
                        var newColumn = new ColumnSnapshot(
                            ColumnName: op.Column!,
                            DataType: op.SqlType ?? "unknown",
                            UdtName: op.SqlType ?? "unknown",
                            CharacterMaximumLength: op.CharacterMaximumLength,
                            NumericPrecision: op.NumericPrecision,
                            NumericScale: op.NumericScale,
                            DateTimePrecision: op.DateTimePrecision,
                            IsNullable: op.IsNullable ?? true,
                            ColumnDefault: op.DefaultSql,
                            IsIdentity: op.IsIdentity ?? false,
                            IdentityGeneration: op.IdentityGeneration,
                            IsGenerated: false,
                            GenerationExpression: null,
                            IsPrimaryKey: op.IsPrimaryKey ?? false,
                            PkConstraintName: op.IsPrimaryKey == true ? $"{op.Table}_pkey" : null,
                            IsUnique: false,
                            UniqueConstraintName: null,
                            IsForeignKey: op.RefTable != null,
                            FkConstraintName: op.RefTable != null ? op.ConstraintName : null,
                            ReferencesSchema: op.RefSchema,
                            ReferencesTable: op.RefTable,
                            ReferencesColumn: op.RefColumn,
                            FkUpdateRule: op.RefTable != null ? "NO ACTION" : null,
                            FkDeleteRule: op.OnDeleteRule
                        );

                        var updatedColumns = tableToAddColumn.Columns.Append(newColumn).ToList();
                        tables[tables.IndexOf(tableToAddColumn)] = tableToAddColumn with { Columns = updatedColumns };
                    }

                    break;

                case MigrationOperationType.DropColumn:
                    var tableToDropColumn = tables.FirstOrDefault(t => t.Schema == op.Schema && t.Name == op.Table);
                    if (tableToDropColumn != null)
                    {
                        var updatedColumns = tableToDropColumn.Columns
                            .Where(c => c.ColumnName != op.Column)
                            .ToList();
                        tables[tables.IndexOf(tableToDropColumn)] = tableToDropColumn with { Columns = updatedColumns };
                    }

                    break;

                case MigrationOperationType.AlterColumnType:
                    var tableToAlterType = tables.FirstOrDefault(t => t.Schema == op.Schema && t.Name == op.Table);
                    if (tableToAlterType != null)
                    {
                        var columnToAlter = tableToAlterType.Columns.FirstOrDefault(c => c.ColumnName == op.Column);
                        if (columnToAlter != null)
                        {
                            var updatedColumn = columnToAlter with
                            {
                                DataType = op.SqlType ?? columnToAlter.DataType,
                                UdtName = op.SqlType ?? columnToAlter.UdtName,
                                CharacterMaximumLength =
                                op.CharacterMaximumLength ?? columnToAlter.CharacterMaximumLength,
                                NumericPrecision = op.NumericPrecision ?? columnToAlter.NumericPrecision,
                                NumericScale = op.NumericScale ?? columnToAlter.NumericScale,
                                DateTimePrecision = op.DateTimePrecision ?? columnToAlter.DateTimePrecision
                            };

                            var updatedColumns = tableToAlterType.Columns
                                .Select(c => c.ColumnName == op.Column ? updatedColumn : c)
                                .ToList();
                            tables[tables.IndexOf(tableToAlterType)] =
                                tableToAlterType with { Columns = updatedColumns };
                        }
                    }

                    break;

                case MigrationOperationType.AlterNullability:
                    var tableToAlterNull = tables.FirstOrDefault(t => t.Schema == op.Schema && t.Name == op.Table);
                    if (tableToAlterNull != null)
                    {
                        var columnToAlter = tableToAlterNull.Columns.FirstOrDefault(c => c.ColumnName == op.Column);
                        if (columnToAlter != null)
                        {
                            var updatedColumn = columnToAlter with
                            {
                                IsNullable = op.IsNullable ?? columnToAlter.IsNullable
                            };
                            var updatedColumns = tableToAlterNull.Columns
                                .Select(c => c.ColumnName == op.Column ? updatedColumn : c)
                                .ToList();
                            tables[tables.IndexOf(tableToAlterNull)] =
                                tableToAlterNull with { Columns = updatedColumns };
                        }
                    }

                    break;

                case MigrationOperationType.SetDefault:
                    var tableToSetDefault = tables.FirstOrDefault(t => t.Schema == op.Schema && t.Name == op.Table);
                    if (tableToSetDefault != null)
                    {
                        var columnToAlter = tableToSetDefault.Columns.FirstOrDefault(c => c.ColumnName == op.Column);
                        if (columnToAlter != null)
                        {
                            var updatedColumn = columnToAlter with { ColumnDefault = op.DefaultSql };
                            var updatedColumns = tableToSetDefault.Columns
                                .Select(c => c.ColumnName == op.Column ? updatedColumn : c)
                                .ToList();
                            tables[tables.IndexOf(tableToSetDefault)] =
                                tableToSetDefault with { Columns = updatedColumns };
                        }
                    }

                    break;

                case MigrationOperationType.DropDefault:
                    var tableToDropDefault = tables.FirstOrDefault(t => t.Schema == op.Schema && t.Name == op.Table);
                    if (tableToDropDefault != null)
                    {
                        var columnToAlter = tableToDropDefault.Columns.FirstOrDefault(c => c.ColumnName == op.Column);
                        if (columnToAlter != null)
                        {
                            var updatedColumn = columnToAlter with { ColumnDefault = null };
                            var updatedColumns = tableToDropDefault.Columns
                                .Select(c => c.ColumnName == op.Column ? updatedColumn : c)
                                .ToList();
                            tables[tables.IndexOf(tableToDropDefault)] =
                                tableToDropDefault with { Columns = updatedColumns };
                        }
                    }

                    break;

                case MigrationOperationType.AddUnique:
                    var tableToAddUnique = tables.FirstOrDefault(t => t.Schema == op.Schema && t.Name == op.Table);
                    if (tableToAddUnique != null)
                    {
                        var columnToAlter = tableToAddUnique.Columns.FirstOrDefault(c => c.ColumnName == op.Column);
                        if (columnToAlter != null)
                        {
                            var updatedColumn = columnToAlter with
                            {
                                IsUnique = true,
                                UniqueConstraintName = op.ConstraintName
                            };
                            var updatedColumns = tableToAddUnique.Columns
                                .Select(c => c.ColumnName == op.Column ? updatedColumn : c)
                                .ToList();
                            tables[tables.IndexOf(tableToAddUnique)] =
                                tableToAddUnique with { Columns = updatedColumns };
                        }
                    }

                    break;

                case MigrationOperationType.DropConstraint:
                    var tableToDropConstraint = tables.FirstOrDefault(t => t.Schema == op.Schema && t.Name == op.Table);
                    if (tableToDropConstraint != null)
                    {
                        var updatedColumns = tableToDropConstraint.Columns.Select(c =>
                        {
                            if (c.UniqueConstraintName == op.ConstraintName)
                                return c with { IsUnique = false, UniqueConstraintName = null };
                            if (c.FkConstraintName == op.ConstraintName)
                                return c with
                                {
                                    IsForeignKey = false,
                                    FkConstraintName = null,
                                    ReferencesSchema = null,
                                    ReferencesTable = null,
                                    ReferencesColumn = null,
                                    FkUpdateRule = null,
                                    FkDeleteRule = null
                                };
                            return c;
                        }).ToList();
                        tables[tables.IndexOf(tableToDropConstraint)] =
                            tableToDropConstraint with { Columns = updatedColumns };
                    }

                    break;

                case MigrationOperationType.AddForeignKey:
                    var tableToAddFk = tables.FirstOrDefault(t => t.Schema == op.Schema && t.Name == op.Table);
                    if (tableToAddFk != null)
                    {
                        var columnToAlter = tableToAddFk.Columns.FirstOrDefault(c => c.ColumnName == op.Column);
                        if (columnToAlter != null)
                        {
                            var updatedColumn = columnToAlter with
                            {
                                IsForeignKey = true,
                                FkConstraintName = op.ConstraintName,
                                ReferencesSchema = op.RefSchema,
                                ReferencesTable = op.RefTable,
                                ReferencesColumn = op.RefColumn,
                                FkDeleteRule = op.OnDeleteRule
                            };
                            var updatedColumns = tableToAddFk.Columns
                                .Select(c => c.ColumnName == op.Column ? updatedColumn : c)
                                .ToList();
                            tables[tables.IndexOf(tableToAddFk)] = tableToAddFk with { Columns = updatedColumns };
                        }
                    }

                    break;
            }
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