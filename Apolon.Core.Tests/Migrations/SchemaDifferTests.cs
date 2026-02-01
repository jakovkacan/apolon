using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;
using Xunit;

namespace Apolon.Core.Tests.Migrations;

public class SchemaDifferTests
{
    [Fact]
    public void Diff_MissingTable_AddsCreateSchemaAndCreateTable()
    {
        var expected = new SchemaSnapshot([
            Table("public", "users", Column("id", isPrimaryKey: true, isIdentity: true))
        ]);
        var actual = new SchemaSnapshot([]);

        var ops = SchemaDiffer.Diff(expected, actual);

        Assert.Collection(ops,
            op =>
            {
                Assert.Equal(MigrationOperationType.CreateSchema, op.Type);
                Assert.Equal("public", op.Schema);
                Assert.Equal("users", op.Table);
            },
            op =>
            {
                Assert.Equal(MigrationOperationType.CreateTable, op.Type);
                Assert.Equal("public", op.Schema);
                Assert.Equal("users", op.Table);
            },
            op =>
            {
                Assert.Equal(MigrationOperationType.AddColumn, op.Type);
                Assert.Equal("public", op.Schema);
                Assert.Equal("users", op.Table);
                Assert.Equal("id", op.Column);
                Assert.Equal("int4", op.SqlType);
                Assert.False(op.IsNullable);
                Assert.True(op.IsPrimaryKey);
                Assert.True(op.IsIdentity);
            });
    }

    [Fact]
    public void Diff_MissingColumn_AddsColumnUniqueAndForeignKey()
    {
        var expected = new SchemaSnapshot([
            Table("public", "users", Column("id")),
            Table("public", "roles", Column("id"))
        ]);

        var expectedWithRoleId = new SchemaSnapshot([
            Table("public", "users",
                Column("id"),
                Column(
                    "role_id",
                    dataType: "int4",
                    isNullable: false,
                    columnDefault: "1",
                    isUnique: true,
                    uniqueName: "users_role_id_key",
                    isForeignKey: true,
                    fkName: "fk_users_role_id",
                    refSchema: "public",
                    refTable: "roles",
                    refColumn: "id",
                    fkDelete: "cascade"
                )),
            Table("public", "roles", Column("id"))
        ]);

        var ops = SchemaDiffer.Diff(expectedWithRoleId, expected);

        Assert.Collection(ops,
            op =>
            {
                Assert.Equal(MigrationOperationType.AddColumn, op.Type);
                Assert.Equal("public", op.Schema);
                Assert.Equal("users", op.Table);
                Assert.Equal("role_id", op.Column);
                Assert.Equal("int4", op.SqlType);
                Assert.False(op.IsNullable);
                Assert.Equal("1", op.DefaultSql);
            },
            op =>
            {
                Assert.Equal(MigrationOperationType.AddUnique, op.Type);
                Assert.Equal("public", op.Schema);
                Assert.Equal("users", op.Table);
                Assert.Equal("role_id", op.Column);
            },
            op =>
            {
                Assert.Equal(MigrationOperationType.AddForeignKey, op.Type);
                Assert.Equal("public", op.Schema);
                Assert.Equal("users", op.Table);
                Assert.Equal("role_id", op.Column);
                Assert.Equal("fk_users_role_id", op.ConstraintName);
                Assert.Equal("public", op.RefSchema);
                Assert.Equal("roles", op.RefTable);
                Assert.Equal("id", op.RefColumn);
                Assert.Equal("cascade", op.OnDeleteRule);
            });
    }

    [Fact]
    public void Diff_ColumnTypeNullabilityAndDefault_ProducesAlterAndSetDefault()
    {
        var expected = new SchemaSnapshot([
            Table("public", "users",
                Column("name", dataType: "varchar", isNullable: false, columnDefault: "'n/a'"))
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "users",
                Column("name", dataType: "int4", isNullable: true, columnDefault: null))
        ]);

        var ops = SchemaDiffer.Diff(expected, actual);

        Assert.Collection(ops,
            op =>
            {
                Assert.Equal(MigrationOperationType.AlterColumnType, op.Type);
                Assert.Equal("users", op.Table);
                Assert.Equal("name", op.Column);
                Assert.Equal("varchar", op.SqlType);
            },
            op =>
            {
                Assert.Equal(MigrationOperationType.AlterNullability, op.Type);
                Assert.Equal("users", op.Table);
                Assert.Equal("name", op.Column);
                Assert.False(op.IsNullable);
            },
            op =>
            {
                Assert.Equal(MigrationOperationType.SetDefault, op.Type);
                Assert.Equal("users", op.Table);
                Assert.Equal("name", op.Column);
                Assert.Equal("'n/a'", op.DefaultSql);
            });
    }

    [Fact]
    public void Diff_ColumnTypeParameters_ProducesAlterWithSqlTypeDetails()
    {
        var expectedCol = Column("name", dataType: "varchar", charMaxLen: 25);
        var actualCol = Column("name", dataType: "varchar", charMaxLen: 50);

        Assert.Equal(25, expectedCol.CharacterMaximumLength);
        Assert.Equal(50, actualCol.CharacterMaximumLength);
        Assert.NotEqual(
            MigrationOperation.BuildSqlType(
                expectedCol.DataType,
                expectedCol.CharacterMaximumLength,
                expectedCol.NumericPrecision,
                expectedCol.NumericScale,
                expectedCol.DateTimePrecision),
            MigrationOperation.BuildSqlType(
                actualCol.DataType,
                actualCol.CharacterMaximumLength,
                actualCol.NumericPrecision,
                actualCol.NumericScale,
                actualCol.DateTimePrecision));

        var expected = new SchemaSnapshot([
            Table("public", "users", expectedCol)
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "users", actualCol)
        ]);

        var ops = SchemaDiffer.Diff(expected, actual);

        var op = Assert.Single(ops, x => x.Type == MigrationOperationType.AlterColumnType);
        Assert.Equal("users", op.Table);
        Assert.Equal("name", op.Column);
        Assert.Equal("varchar", op.SqlType);
        Assert.Equal(25, op.CharacterMaximumLength);
        Assert.Equal("VARCHAR(25)", op.GetSqlType());
    }

    [Fact]
    public void Diff_DefaultRemoved_ProducesDropDefault()
    {
        var expected = new SchemaSnapshot([
            Table("public", "users",
                Column("status", dataType: "varchar", isNullable: true, columnDefault: null))
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "users",
                Column("status", dataType: "varchar", isNullable: true, columnDefault: "'active'"))
        ]);

        var ops = SchemaDiffer.Diff(expected, actual);

        var op = Assert.Single(ops);
        Assert.Equal(MigrationOperationType.DropDefault, op.Type);
        Assert.Equal("users", op.Table);
        Assert.Equal("status", op.Column);
    }

    [Fact]
    public void Diff_ColumnBecomesForeignKey_ProducesAddForeignKey()
    {
        var expected = new SchemaSnapshot([
            Table("public", "checkups", Column("id")),
            Table("public", "tests",
                Column("id"),
                Column(
                    "checkup_id",
                    isForeignKey: true,
                    fkName: "fk_tests_checkup_id",
                    refSchema: "public",
                    refTable: "checkups",
                    refColumn: "id",
                    fkDelete: "cascade"
                ))
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "checkups", Column("id")),
            Table("public", "tests",
                Column("id"),
                Column("checkup_id"))
        ]);

        var ops = SchemaDiffer.Diff(expected, actual);

        var op = Assert.Single(ops, x => x.Type == MigrationOperationType.AddForeignKey);
        Assert.Equal("public", op.Schema);
        Assert.Equal("tests", op.Table);
        Assert.Equal("checkup_id", op.Column);
        Assert.Equal("fk_tests_checkup_id", op.ConstraintName);
        Assert.Equal("public", op.RefSchema);
        Assert.Equal("checkups", op.RefTable);
        Assert.Equal("id", op.RefColumn);
        Assert.Equal("cascade", op.OnDeleteRule);
    }

    [Fact]
    public void Diff_TableRemoved_ProducesDropTable()
    {
        var expected = new SchemaSnapshot([
            Table("public", "users", Column("id"))
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "users", Column("id")),
            Table("public", "old_table", Column("id"))
        ]);

        var ops = SchemaDiffer.Diff(expected, actual);

        var op = Assert.Single(ops);
        Assert.Equal(MigrationOperationType.DropTable, op.Type);
        Assert.Equal("public", op.Schema);
        Assert.Equal("old_table", op.Table);
    }

    [Fact]
    public void Diff_ColumnRemoved_ProducesDropColumn()
    {
        var expected = new SchemaSnapshot([
            Table("public", "users",
                Column("id"),
                Column("name"))
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "users",
                Column("id"),
                Column("name"),
                Column("old_column", dataType: "varchar"))
        ]);

        var ops = SchemaDiffer.Diff(expected, actual);

        var op = Assert.Single(ops);
        Assert.Equal(MigrationOperationType.DropColumn, op.Type);
        Assert.Equal("public", op.Schema);
        Assert.Equal("users", op.Table);
        Assert.Equal("old_column", op.Column);
    }

    [Fact]
    public void Diff_ForeignKeyChanged_ProducesDropConstraintThenAddForeignKey()
    {
        var expected = new SchemaSnapshot([
            Table("public", "checkups", Column("id")),
            Table("public", "patients", Column("id")),
            Table("public", "tests",
                Column("id"),
                Column(
                    "patient_id",
                    isForeignKey: true,
                    fkName: "fk_tests_patient_id",
                    refSchema: "public",
                    refTable: "patients",
                    refColumn: "id",
                    fkDelete: "cascade"
                ))
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "checkups", Column("id")),
            Table("public", "patients", Column("id")),
            Table("public", "tests",
                Column("id"),
                Column(
                    "patient_id",
                    isForeignKey: true,
                    fkName: "fk_tests_patient_id_old",
                    refSchema: "public",
                    refTable: "checkups",
                    refColumn: "id",
                    fkDelete: "restrict"
                ))
        ]);

        var ops = SchemaDiffer.Diff(expected, actual);

        Assert.Collection(ops,
            op =>
            {
                Assert.Equal(MigrationOperationType.DropConstraint, op.Type);
                Assert.Equal("public", op.Schema);
                Assert.Equal("tests", op.Table);
                Assert.Equal("fk_tests_patient_id_old", op.ConstraintName);
            },
            op =>
            {
                Assert.Equal(MigrationOperationType.AddForeignKey, op.Type);
                Assert.Equal("public", op.Schema);
                Assert.Equal("tests", op.Table);
                Assert.Equal("patient_id", op.Column);
                Assert.Equal("fk_tests_patient_id", op.ConstraintName);
                Assert.Equal("public", op.RefSchema);
                Assert.Equal("patients", op.RefTable);
                Assert.Equal("id", op.RefColumn);
                Assert.Equal("cascade", op.OnDeleteRule);
            });
    }

    [Fact]
    public void Diff_WithCommittedOperations_FiltersOutCommittedOps()
    {
        var expected = new SchemaSnapshot([
            Table("public", "users",
                Column("id"),
                Column("name"),
                Column("email"))
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "users",
                Column("id"))
        ]);

        var committedOps = new List<MigrationOperation>
        {
            new(
                Type: MigrationOperationType.AddColumn,
                Schema: "public",
                Table: "users",
                Column: "name",
                SqlType: "int4",
                CharacterMaximumLength: null,
                NumericPrecision: null,
                NumericScale: null,
                DateTimePrecision: null,
                IsPrimaryKey: false,
                IsIdentity: false,
                IdentityGeneration: null,
                IsNullable: false,
                DefaultSql: null
            )
        };

        var ops = SchemaDiffer.Diff(expected, actual, committedOps);

        // Should only include the email column add, not the name column add
        var op = Assert.Single(ops);
        Assert.Equal(MigrationOperationType.AddColumn, op.Type);
        Assert.Equal("email", op.Column);
    }

    [Fact]
    public void Diff_ForeignKeyRemovedWithoutConstraintName_DoesNotDropConstraint()
    {
        var expected = new SchemaSnapshot([
            Table("public", "users", Column("id")),
            Table("public", "orders",
                Column("id"),
                Column("user_id"))
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "users", Column("id")),
            Table("public", "orders",
                Column("id"),
                Column(
                    "user_id",
                    isForeignKey: true,
                    fkName: null, // No constraint name
                    refSchema: "public",
                    refTable: "users",
                    refColumn: "id"
                ))
        ]);

        var ops = SchemaDiffer.Diff(expected, actual);

        // Should not produce a DropConstraint operation since there's no constraint name
        Assert.DoesNotContain(ops, op => op.Type == MigrationOperationType.DropConstraint);
    }

    [Fact]
    public void Diff_ForeignKeyRemovedWithEmptyConstraintName_DoesNotDropConstraint()
    {
        var expected = new SchemaSnapshot([
            Table("public", "users", Column("id")),
            Table("public", "orders",
                Column("id"),
                Column("user_id"))
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "users", Column("id")),
            Table("public", "orders",
                Column("id"),
                Column(
                    "user_id",
                    isForeignKey: true,
                    fkName: "", // Empty constraint name
                    refSchema: "public",
                    refTable: "users",
                    refColumn: "id"
                ))
        ]);

        var ops = SchemaDiffer.Diff(expected, actual);

        // Should not produce a DropConstraint operation since constraint name is empty
        Assert.DoesNotContain(ops, op => op.Type == MigrationOperationType.DropConstraint);
    }

    [Fact]
    public void Diff_MultipleTablesAndColumnsDropped_ProducesMultipleDropOperations()
    {
        var expected = new SchemaSnapshot([
            Table("public", "users", Column("id"), Column("name"))
        ]);

        var actual = new SchemaSnapshot([
            Table("public", "users", Column("id"), Column("name"), Column("email")),
            Table("public", "legacy_table", Column("id")),
            Table("public", "temp_table", Column("id"))
        ]);

        var ops = SchemaDiffer.Diff(expected, actual);

        Assert.Contains(ops, op => op.Type == MigrationOperationType.DropTable && op.Table == "legacy_table");
        Assert.Contains(ops, op => op.Type == MigrationOperationType.DropTable && op.Table == "temp_table");
        Assert.Contains(ops, op => op.Type == MigrationOperationType.DropColumn && op.Column == "email");
    }

    private static TableSnapshot Table(string schema, string table, params ColumnSnapshot[] cols)
        => new(schema, table, cols);

    private static ColumnSnapshot Column(
        string name,
        string dataType = "int4",
        bool isNullable = false,
        string? columnDefault = null,
        bool isUnique = false,
        string? uniqueName = null,
        bool isForeignKey = false,
        string? fkName = null,
        string? refSchema = null,
        string? refTable = null,
        string? refColumn = null,
        string? fkDelete = null,
        int? charMaxLen = null,
        int? numericPrecision = null,
        int? numericScale = null,
        int? dateTimePrecision = null,
        bool isPrimaryKey = false,
        bool isIdentity = false,
        string? identityGeneration = null)
        => new(
            ColumnName: name,
            DataType: dataType,
            UdtName: dataType,
            CharacterMaximumLength: charMaxLen,
            NumericPrecision: numericPrecision,
            NumericScale: numericScale,
            DateTimePrecision: dateTimePrecision,
            IsNullable: isNullable,
            ColumnDefault: columnDefault,
            IsIdentity: isIdentity,
            IdentityGeneration: identityGeneration,
            IsGenerated: false,
            GenerationExpression: null,
            IsPrimaryKey: isPrimaryKey,
            PkConstraintName: null,
            IsUnique: isUnique,
            UniqueConstraintName: uniqueName,
            IsForeignKey: isForeignKey,
            FkConstraintName: fkName,
            ReferencesSchema: refSchema,
            ReferencesTable: refTable,
            ReferencesColumn: refColumn,
            FkUpdateRule: null,
            FkDeleteRule: fkDelete
        );
}
