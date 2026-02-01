using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations;

internal static class SchemaDiffer
{
    public static IReadOnlyList<MigrationOperation> Diff(SchemaSnapshot expected, SchemaSnapshot actual,
        IReadOnlyList<MigrationOperation>? commitedOperations = null)
    {
        var ops = new List<MigrationOperation>();

        var actualTables = actual.Tables.ToDictionary(t => (t.Schema, Table: t.Name));
        var expectedTables = expected.Tables.ToDictionary(t => (t.Schema, Table: t.Name));

        // Remove migration history table from actual tables
        var migrationSnapshotTable = SnapshotBuilder.BuildFromModel(typeof(MigrationHistoryTable)).Tables[0];
        actualTables.Remove((migrationSnapshotTable.Schema, migrationSnapshotTable.Name));

        // 1) Create schemas / tables that are missing
        foreach (var (key, expTable) in expectedTables)
        {
            if (actualTables.ContainsKey(key)) continue;

            ops.Add(new MigrationOperation(MigrationOperationType.CreateSchema, expTable.Schema, expTable.Name));
            ops.Add(new MigrationOperation(MigrationOperationType.CreateTable, expTable.Schema, expTable.Name));

            // Add all columns for newly created tables.
            foreach (var expCol in expTable.Columns) AddColumnOps(ops, expTable, expCol);
        }

        // 1b) Drop tables that no longer exist in the model
        foreach (var (key, actTable) in actualTables)
        {
            if (expectedTables.ContainsKey(key)) continue;

            ops.Add(new MigrationOperation(
                MigrationOperationType.DropTable,
                actTable.Schema,
                actTable.Name
            ));
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
                if (actCols.ContainsKey(colName)) continue;

                AddColumnOps(ops, expTable, expCol);
            }

            // Drop columns removed from model
            foreach (var (colName, actCol) in actCols)
            {
                if (expCols.ContainsKey(colName)) continue;

                ops.Add(new MigrationOperation(
                    MigrationOperationType.DropColumn,
                    expTable.Schema,
                    expTable.Name,
                    actCol.ColumnName
                ));
            }

            // Alter existing columns (type/nullability/default)
            foreach (var (colName, expCol) in expCols)
            {
                if (!actCols.TryGetValue(colName, out var actCol))
                    continue;

                if (IsTypeDifferent(expCol, actCol))
                    ops.Add(new MigrationOperation(
                        MigrationOperationType.AlterColumnType,
                        expTable.Schema,
                        expTable.Name,
                        colName,
                        expCol.DataType,
                        expCol.CharacterMaximumLength,
                        expCol.NumericPrecision,
                        expCol.NumericScale,
                        expCol.DateTimePrecision
                    ));

                if (expCol.IsNullable != actCol.IsNullable)
                    ops.Add(new MigrationOperation(
                        MigrationOperationType.AlterNullability,
                        expTable.Schema,
                        expTable.Name,
                        colName,
                        IsNullable: expCol.IsNullable
                    ));

                // Default compare (normalized strings)
                if (!string.Equals(expCol.ColumnDefault, actCol.ColumnDefault, StringComparison.Ordinal))
                {
                    if (expCol.ColumnDefault is null)
                        ops.Add(new MigrationOperation(
                            MigrationOperationType.DropDefault,
                            expTable.Schema,
                            expTable.Name,
                            colName
                        ));
                    else
                        ops.Add(new MigrationOperation(
                            MigrationOperationType.SetDefault,
                            expTable.Schema,
                            expTable.Name,
                            colName,
                            DefaultSql: expCol.ColumnDefault
                        ));
                }

                var fkMismatch =
                    expCol.IsForeignKey != actCol.IsForeignKey ||
                    (expCol.IsForeignKey && actCol.IsForeignKey && (
                        !string.Equals(expCol.ReferencesSchema, actCol.ReferencesSchema, StringComparison.Ordinal) ||
                        !string.Equals(expCol.ReferencesTable, actCol.ReferencesTable, StringComparison.Ordinal) ||
                        !string.Equals(expCol.ReferencesColumn, actCol.ReferencesColumn, StringComparison.Ordinal) ||
                        !string.Equals(expCol.FkDeleteRule, actCol.FkDeleteRule, StringComparison.Ordinal)
                    ));

                if (!fkMismatch) continue;

                if (actCol.IsForeignKey && !string.IsNullOrWhiteSpace(actCol.FkConstraintName))
                    ops.Add(new MigrationOperation(
                        MigrationOperationType.DropConstraint,
                        expTable.Schema,
                        expTable.Name,
                        ConstraintName: actCol.FkConstraintName
                    ));

                if (expCol.IsForeignKey)
                    ops.Add(new MigrationOperation(
                        MigrationOperationType.AddForeignKey,
                        expTable.Schema,
                        expTable.Name,
                        colName,
                        ConstraintName: expCol.FkConstraintName,
                        RefSchema: expCol.ReferencesSchema,
                        RefTable: expCol.ReferencesTable,
                        RefColumn: expCol.ReferencesColumn,
                        OnDeleteRule: expCol.FkDeleteRule // may be null model-side; builder can default
                    ));
            }
        }

        if (commitedOperations != null) ops.RemoveAll(commitedOperations.Contains);

        // Important ordering note:
        // - Create schemas/tables first
        // - Add/alter columns next
        // - Constraints (unique/FK) last
        return ops;
    }

    private static void AddColumnOps(
        List<MigrationOperation> ops,
        TableSnapshot expTable,
        ColumnSnapshot expCol)
    {
        ops.Add(new MigrationOperation(
            MigrationOperationType.AddColumn,
            expTable.Schema,
            expTable.Name,
            expCol.ColumnName,
            expCol.DataType,
            expCol.CharacterMaximumLength,
            expCol.NumericPrecision,
            expCol.NumericScale,
            expCol.DateTimePrecision,
            expCol.IsPrimaryKey,
            expCol.IsIdentity,
            expCol.IdentityGeneration,
            expCol.IsNullable,
            expCol.ColumnDefault
        ));

        if (expCol.IsUnique)
            ops.Add(new MigrationOperation(
                MigrationOperationType.AddUnique,
                expTable.Schema,
                expTable.Name,
                expCol.ColumnName
            ));

        if (expCol.IsForeignKey)
            ops.Add(new MigrationOperation(
                MigrationOperationType.AddForeignKey,
                expTable.Schema,
                expTable.Name,
                expCol.ColumnName,
                ConstraintName: expCol.FkConstraintName,
                RefSchema: expCol.ReferencesSchema,
                RefTable: expCol.ReferencesTable,
                RefColumn: expCol.ReferencesColumn,
                OnDeleteRule: expCol.FkDeleteRule // may be null model-side; builder can default
            ));
    }

    private static bool IsTypeDifferent(ColumnSnapshot expected, ColumnSnapshot actual)
    {
        var expectedType = MigrationOperation.BuildSqlType(
            expected.DataType,
            expected.CharacterMaximumLength,
            expected.NumericPrecision,
            expected.NumericScale,
            expected.DateTimePrecision);

        var actualType = MigrationOperation.BuildSqlType(
            actual.DataType,
            actual.CharacterMaximumLength,
            actual.NumericPrecision,
            actual.NumericScale,
            actual.DateTimePrecision);

        return !string.Equals(expectedType, actualType, StringComparison.Ordinal);
    }
}