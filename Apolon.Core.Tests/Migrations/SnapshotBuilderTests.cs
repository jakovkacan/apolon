using Apolon.Core.Attributes;
using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Tests.Migrations;

public class SnapshotBuilderTests
{
    [Fact]
    public void BuildFromModel_SingleEntityWithPrimaryKey_CreatesTableWithIdentityColumn()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(SimpleEntity));

        var table = Assert.Single(snapshot.Tables);
        Assert.Equal("public", table.Schema);
        Assert.Equal("simple_entities", table.Name);

        var pkColumn = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
        Assert.NotNull(pkColumn);
        Assert.Equal("id", pkColumn.ColumnName);
        Assert.True(pkColumn.IsIdentity);
        Assert.Equal("always", pkColumn.IdentityGeneration);
        Assert.Equal("simple_entities_pkey", pkColumn.PkConstraintName);
    }

    [Fact]
    public void BuildFromModel_EntityWithMultipleColumns_CreatesAllColumns()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(EntityWithMultipleColumns));

        var table = Assert.Single(snapshot.Tables);
        Assert.Equal(4, table.Columns.Count);

        var nameColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "name");
        Assert.NotNull(nameColumn);
        Assert.Equal("varchar", nameColumn.DataType);

        var ageColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "age");
        Assert.NotNull(ageColumn);
        Assert.Equal("int4", ageColumn.DataType);
    }

    [Fact]
    public void BuildFromModel_NullableColumn_SetsIsNullableTrue()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(EntityWithNullableColumn));

        var table = Assert.Single(snapshot.Tables);
        var emailColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "email");
        Assert.NotNull(emailColumn);
        Assert.True(emailColumn.IsNullable);
    }

    [Fact]
    public void BuildFromModel_NonNullableColumn_SetsIsNullableFalse()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(EntityWithMultipleColumns));

        var table = Assert.Single(snapshot.Tables);
        var nameColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "name");
        Assert.NotNull(nameColumn);
        Assert.False(nameColumn.IsNullable);
    }

    [Fact]
    public void BuildFromModel_ColumnWithDefaultValue_SetsColumnDefault()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(EntityWithDefaultValue));

        var table = Assert.Single(snapshot.Tables);
        var statusColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "status");
        Assert.NotNull(statusColumn);
        Assert.Equal("'active'", statusColumn.ColumnDefault);
    }

    [Fact]
    public void BuildFromModel_ColumnWithRawSqlDefault_UsesRawSqlValue()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(EntityWithRawSqlDefault));

        var table = Assert.Single(snapshot.Tables);
        var createdAtColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "created_at");
        Assert.NotNull(createdAtColumn);
        Assert.Equal("current_timestamp", createdAtColumn.ColumnDefault);
    }

    [Fact]
    public void BuildFromModel_UniqueColumn_SetsIsUniqueAndConstraintName()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(EntityWithUniqueColumn));

        var table = Assert.Single(snapshot.Tables);
        var emailColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "email");
        Assert.NotNull(emailColumn);
        Assert.True(emailColumn.IsUnique);
        Assert.Equal("unique_entities_email_key", emailColumn.UniqueConstraintName);
    }

    [Fact]
    public void BuildFromModel_ForeignKeyColumn_SetsForeignKeyMetadata()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(EntityWithForeignKey), typeof(SimpleEntity));

        var table = snapshot.Tables.FirstOrDefault(t => t.Name == "entities_with_fk");
        Assert.NotNull(table);

        var fkColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "simple_entity_id");
        Assert.NotNull(fkColumn);
        Assert.True(fkColumn.IsForeignKey);
        Assert.Equal("entities_with_fk_simple_entity_id_fkey", fkColumn.FkConstraintName);
        Assert.Equal("public", fkColumn.ReferencesSchema);
        Assert.Equal("simple_entities", fkColumn.ReferencesTable);
        Assert.Equal("id", fkColumn.ReferencesColumn);
    }

    [Fact]
    public void BuildFromModel_ForeignKeyWithCascadeDelete_SetsFkDeleteRule()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(EntityWithCascadeDelete), typeof(SimpleEntity));

        var table = snapshot.Tables.FirstOrDefault(t => t.Name == "cascade_entities");
        Assert.NotNull(table);

        var fkColumn = table.Columns.FirstOrDefault(c => c.IsForeignKey);
        Assert.NotNull(fkColumn);
        Assert.Equal("CASCADE", fkColumn.FkDeleteRule);
    }

    [Fact]
    public void BuildFromModel_ForeignKeyWithNoActionDelete_SetsFkDeleteRuleToNoAction()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(EntityWithForeignKey), typeof(SimpleEntity));

        var table = snapshot.Tables.FirstOrDefault(t => t.Name == "entities_with_fk");
        Assert.NotNull(table);

        var fkColumn = table.Columns.FirstOrDefault(c => c.IsForeignKey);
        Assert.NotNull(fkColumn);
        Assert.Equal("NO ACTION", fkColumn.FkDeleteRule);
    }

    [Fact]
    public void BuildFromModel_MultipleEntities_CreatesMultipleTables()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(SimpleEntity), typeof(EntityWithMultipleColumns));

        Assert.Equal(2, snapshot.Tables.Count);
        Assert.Contains(snapshot.Tables, t => t.Name == "simple_entities");
        Assert.Contains(snapshot.Tables, t => t.Name == "entities_with_columns");
    }

    [Fact]
    public void BuildFromModel_ManualPrimaryKey_SetsIsIdentityFalse()
    {
        var snapshot = SnapshotBuilder.BuildFromModel(typeof(EntityWithManualPrimaryKey));

        var table = Assert.Single(snapshot.Tables);
        var pkColumn = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
        Assert.NotNull(pkColumn);
        Assert.False(pkColumn.IsIdentity);
        Assert.Null(pkColumn.IdentityGeneration);
    }

    [Fact]
    public void BuildFromModel_EmptyEntityArray_ReturnsEmptySnapshot()
    {
        var snapshot = SnapshotBuilder.BuildFromModel();

        Assert.Empty(snapshot.Tables);
    }

    [Fact]
    public void ApplyMigrations_CreateSchema_DoesNotAffectSnapshot()
    {
        var initialSnapshot = new SchemaSnapshot([]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.CreateSchema, "public", "")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        Assert.Empty(result.Tables);
    }

    [Fact]
    public void ApplyMigrations_CreateTable_AddsEmptyTable()
    {
        var initialSnapshot = new SchemaSnapshot([]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.CreateTable, "public", "users")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        Assert.Equal("public", table.Schema);
        Assert.Equal("users", table.Name);
        Assert.Empty(table.Columns);
    }

    [Fact]
    public void ApplyMigrations_DropTable_RemovesTable()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id")]),
            new TableSnapshot("public", "orders", [Col("id")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.DropTable, "public", "users")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        Assert.Equal("orders", table.Name);
    }

    [Fact]
    public void ApplyMigrations_AddColumn_AddsColumnToTable()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AddColumn, "public", "users",
                "name", "varchar", IsNullable: false)
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        Assert.Equal(2, table.Columns.Count);
        var nameColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "name");
        Assert.NotNull(nameColumn);
        Assert.Equal("varchar", nameColumn.DataType);
        Assert.False(nameColumn.IsNullable);
    }

    [Fact]
    public void ApplyMigrations_AddColumnWithDefaultValue_SetsColumnDefault()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AddColumn, "public", "users",
                "status", "varchar", IsNullable: false, DefaultSql: "'active'")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var statusColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "status");
        Assert.NotNull(statusColumn);
        Assert.Equal("'active'", statusColumn.ColumnDefault);
    }

    [Fact]
    public void ApplyMigrations_AddColumnWithIdentity_SetsIdentityProperties()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AddColumn, "public", "users",
                "id", "int4", IsNullable: false,
                IsPrimaryKey: true, IsIdentity: true, IdentityGeneration: "always")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var idColumn = Assert.Single(table.Columns);
        Assert.True(idColumn.IsIdentity);
        Assert.Equal("always", idColumn.IdentityGeneration);
        Assert.True(idColumn.IsPrimaryKey);
    }

    [Fact]
    public void ApplyMigrations_AddColumnWithForeignKey_SetsForeignKeyProperties()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "orders", [Col("id")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AddColumn, "public", "orders",
                "user_id", "int4", IsNullable: false,
                RefSchema: "public", RefTable: "users", RefColumn: "id", OnDeleteRule: "CASCADE")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var fkColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "user_id");
        Assert.NotNull(fkColumn);
        Assert.True(fkColumn.IsForeignKey);
        Assert.Equal("public", fkColumn.ReferencesSchema);
        Assert.Equal("users", fkColumn.ReferencesTable);
        Assert.Equal("id", fkColumn.ReferencesColumn);
        Assert.Equal("CASCADE", fkColumn.FkDeleteRule);
    }

    [Fact]
    public void ApplyMigrations_DropColumn_RemovesColumn()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id"), Col("name"), Col("email")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.DropColumn, "public", "users", "email")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        Assert.Equal(2, table.Columns.Count);
        Assert.DoesNotContain(table.Columns, c => c.ColumnName == "email");
    }

    [Fact]
    public void ApplyMigrations_AlterColumnType_ChangesDataType()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id"), Col("age", "int4")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AlterColumnType, "public", "users",
                "age", "bigint")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var ageColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "age");
        Assert.NotNull(ageColumn);
        Assert.Equal("bigint", ageColumn.DataType);
    }

    [Fact]
    public void ApplyMigrations_AlterColumnTypeWithPrecision_UpdatesTypeDetails()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "products", [Col("id"), Col("price", "numeric")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AlterColumnType, "public", "products",
                "price", "numeric", NumericPrecision: 10, NumericScale: 2)
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var priceColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "price");
        Assert.NotNull(priceColumn);
        Assert.Equal(10, priceColumn.NumericPrecision);
        Assert.Equal(2, priceColumn.NumericScale);
    }

    [Fact]
    public void ApplyMigrations_AlterNullability_ChangesIsNullable()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id"), Col("email", isNullable: true)])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AlterNullability, "public", "users",
                "email", IsNullable: false)
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var emailColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "email");
        Assert.NotNull(emailColumn);
        Assert.False(emailColumn.IsNullable);
    }

    [Fact]
    public void ApplyMigrations_SetDefault_SetsColumnDefault()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id"), Col("status")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.SetDefault, "public", "users",
                "status", DefaultSql: "'pending'")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var statusColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "status");
        Assert.NotNull(statusColumn);
        Assert.Equal("'pending'", statusColumn.ColumnDefault);
    }

    [Fact]
    public void ApplyMigrations_DropDefault_RemovesColumnDefault()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id"), Col("status", columnDefault: "'active'")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.DropDefault, "public", "users", "status")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var statusColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "status");
        Assert.NotNull(statusColumn);
        Assert.Null(statusColumn.ColumnDefault);
    }

    [Fact]
    public void ApplyMigrations_AddUnique_SetsUniqueConstraint()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id"), Col("email")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AddUnique, "public", "users",
                "email", ConstraintName: "users_email_key")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var emailColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "email");
        Assert.NotNull(emailColumn);
        Assert.True(emailColumn.IsUnique);
        Assert.Equal("users_email_key", emailColumn.UniqueConstraintName);
    }

    [Fact]
    public void ApplyMigrations_DropConstraint_RemovesUniqueConstraint()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [
                Col("id"),
                Col("email", isUnique: true, uniqueConstraintName: "users_email_key")
            ])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.DropConstraint, "public", "users",
                ConstraintName: "users_email_key")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var emailColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "email");
        Assert.NotNull(emailColumn);
        Assert.False(emailColumn.IsUnique);
        Assert.Null(emailColumn.UniqueConstraintName);
    }

    [Fact]
    public void ApplyMigrations_DropConstraint_RemovesForeignKeyConstraint()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "orders", [
                Col("id"),
                Col("user_id", isForeignKey: true, fkConstraintName: "orders_user_id_fkey",
                    refSchema: "public", refTable: "users", refColumn: "id")
            ])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.DropConstraint, "public", "orders",
                ConstraintName: "orders_user_id_fkey")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var userIdColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "user_id");
        Assert.NotNull(userIdColumn);
        Assert.False(userIdColumn.IsForeignKey);
        Assert.Null(userIdColumn.FkConstraintName);
        Assert.Null(userIdColumn.ReferencesSchema);
        Assert.Null(userIdColumn.ReferencesTable);
        Assert.Null(userIdColumn.ReferencesColumn);
    }

    [Fact]
    public void ApplyMigrations_AddForeignKey_SetsForeignKeyConstraint()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "orders", [Col("id"), Col("user_id")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AddForeignKey, "public", "orders",
                "user_id", ConstraintName: "fk_orders_user",
                RefSchema: "public", RefTable: "users", RefColumn: "id", OnDeleteRule: "SET NULL")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var userIdColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "user_id");
        Assert.NotNull(userIdColumn);
        Assert.True(userIdColumn.IsForeignKey);
        Assert.Equal("fk_orders_user", userIdColumn.FkConstraintName);
        Assert.Equal("public", userIdColumn.ReferencesSchema);
        Assert.Equal("users", userIdColumn.ReferencesTable);
        Assert.Equal("id", userIdColumn.ReferencesColumn);
        Assert.Equal("SET NULL", userIdColumn.FkDeleteRule);
    }

    [Fact]
    public void ApplyMigrations_MultipleOperations_AppliesInOrder()
    {
        var initialSnapshot = new SchemaSnapshot([]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.CreateTable, "public", "users"),
            new(MigrationOperationType.AddColumn, "public", "users",
                "id", "int4", IsNullable: false, IsPrimaryKey: true),
            new(MigrationOperationType.AddColumn, "public", "users",
                "name", "varchar", IsNullable: false),
            new(MigrationOperationType.CreateTable, "public", "orders"),
            new(MigrationOperationType.AddColumn, "public", "orders",
                "id", "int4", IsNullable: false)
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        Assert.Equal(2, result.Tables.Count);
        var usersTable = result.Tables.FirstOrDefault(t => t.Name == "users");
        Assert.NotNull(usersTable);
        Assert.Equal(2, usersTable.Columns.Count);

        var ordersTable = result.Tables.FirstOrDefault(t => t.Name == "orders");
        Assert.NotNull(ordersTable);
        Assert.Single(ordersTable.Columns);
    }

    [Fact]
    public void ApplyMigrations_OperationOnNonExistentTable_DoesNotThrow()
    {
        var initialSnapshot = new SchemaSnapshot([]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AddColumn, "public", "users",
                "name", "varchar", IsNullable: false)
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        Assert.Empty(result.Tables);
    }

    [Fact]
    public void ApplyMigrations_DropNonExistentColumn_DoesNotThrow()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.DropColumn, "public", "users", "nonexistent")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        Assert.Single(table.Columns);
    }

    [Fact]
    public void ApplyMigrations_AlterNonExistentColumn_DoesNotThrow()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.AlterColumnType, "public", "users",
                "nonexistent", "varchar")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        Assert.Single(table.Columns);
    }

    [Fact]
    public void ApplyMigrations_EmptyOperationsList_ReturnsUnchangedSnapshot()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id"), Col("name")])
        ]);
        var operations = new List<MigrationOperation>();

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        Assert.Equal(initialSnapshot.Tables.Count, result.Tables.Count);
        var table = Assert.Single(result.Tables);
        Assert.Equal(2, table.Columns.Count);
    }

    [Fact]
    public void ApplyMigrations_CreateAndDropSameTable_ResultsInNoTable()
    {
        var initialSnapshot = new SchemaSnapshot([]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.CreateTable, "public", "temp_table"),
            new(MigrationOperationType.DropTable, "public", "temp_table")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        Assert.Empty(result.Tables);
    }

    [Fact]
    public void ApplyMigrations_SetAndDropDefault_ResultsInNoDefault()
    {
        var initialSnapshot = new SchemaSnapshot([
            new TableSnapshot("public", "users", [Col("id"), Col("status")])
        ]);
        var operations = new List<MigrationOperation>
        {
            new(MigrationOperationType.SetDefault, "public", "users",
                "status", DefaultSql: "'active'"),
            new(MigrationOperationType.DropDefault, "public", "users", "status")
        };

        var result = SnapshotBuilder.ApplyMigrations(initialSnapshot, operations);

        var table = Assert.Single(result.Tables);
        var statusColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "status");
        Assert.NotNull(statusColumn);
        Assert.Null(statusColumn.ColumnDefault);
    }

    private static ColumnSnapshot Col(
        string name,
        string dataType = "int4",
        bool isNullable = false,
        string? columnDefault = null,
        bool isUnique = false,
        string? uniqueConstraintName = null,
        bool isForeignKey = false,
        string? fkConstraintName = null,
        string? refSchema = null,
        string? refTable = null,
        string? refColumn = null)
    {
        return new ColumnSnapshot(
            name,
            dataType,
            dataType,
            null,
            null,
            null,
            null,
            isNullable,
            columnDefault,
            false,
            null,
            false,
            null,
            false,
            null,
            isUnique,
            uniqueConstraintName,
            isForeignKey,
            fkConstraintName,
            refSchema,
            refTable,
            refColumn,
            null,
            null
        );
    }
}

// Test entity models for BuildFromModel tests

[Table("simple_entities", Schema = "public")]
public class SimpleEntity
{
    [PrimaryKey] public int Id { get; set; }
}

[Table("entities_with_columns", Schema = "public")]
public class EntityWithMultipleColumns
{
    [PrimaryKey] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool Active { get; set; }
}

[Table("nullable_entities", Schema = "public")]
public class EntityWithNullableColumn
{
    [PrimaryKey] public int Id { get; set; }
    public string? Email { get; set; }
}

[Table("default_entities", Schema = "public")]
public class EntityWithDefaultValue
{
    [PrimaryKey] public int Id { get; set; }

    [Column("status", DefaultValue = "active")]
    public string Status { get; set; } = string.Empty;
}

[Table("raw_default_entities", Schema = "public")]
public class EntityWithRawSqlDefault
{
    [PrimaryKey] public int Id { get; set; }

    [Column("created_at", DefaultValue = "NOW()", DefaultIsRawSql = true)]
    public DateTime CreatedAt { get; set; }
}

[Table("unique_entities", Schema = "public")]
public class EntityWithUniqueColumn
{
    [PrimaryKey] public int Id { get; set; }

    [Unique]
    [Column("email")] public string Email { get; set; } = string.Empty;
}

[Table("entities_with_fk", Schema = "public")]
public class EntityWithForeignKey
{
    [PrimaryKey] public int Id { get; set; }

    [ForeignKey(typeof(SimpleEntity), "id")]
    public int SimpleEntityId { get; set; }
}

[Table("cascade_entities", Schema = "public")]
public class EntityWithCascadeDelete
{
    [PrimaryKey] public int Id { get; set; }

    [ForeignKey(typeof(SimpleEntity), "id", OnDeleteBehavior = OnDeleteBehavior.Cascade)]
    public int SimpleEntityId { get; set; }
}

[Table("manual_pk_entities", Schema = "public")]
public class EntityWithManualPrimaryKey
{
    [PrimaryKey(AutoIncrement = false)] public Guid Id { get; set; }
}