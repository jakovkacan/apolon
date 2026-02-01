using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Tests.Migrations;

public class MigrationBuilderTests
{
    [Fact]
    public void CreateSchema_AddsCreateSchemaOperation()
    {
        var builder = new MigrationBuilder();

        builder.CreateSchema("custom_schema");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.CreateSchema, operation.Type);
        Assert.Equal("custom_schema", operation.Schema);
        Assert.Equal("", operation.Table);
    }

    [Fact]
    public void CreateSchema_MultipleSchemas_AddsMultipleOperations()
    {
        var builder = new MigrationBuilder();

        builder.CreateSchema("schema1");
        builder.CreateSchema("schema2");

        Assert.Equal(2, builder.Operations.Count);
        Assert.Equal("schema1", builder.Operations[0].Schema);
        Assert.Equal("schema2", builder.Operations[1].Schema);
    }

    [Fact]
    public void CreateTable_AddsCreateTableOperation()
    {
        var builder = new MigrationBuilder();

        builder.CreateTable("public", "users");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.CreateTable, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("users", operation.Table);
    }

    [Fact]
    public void CreateTable_DifferentSchemas_AddsCorrectOperations()
    {
        var builder = new MigrationBuilder();

        builder.CreateTable("public", "users");
        builder.CreateTable("admin", "logs");

        Assert.Equal(2, builder.Operations.Count);
        Assert.Equal("public", builder.Operations[0].Schema);
        Assert.Equal("users", builder.Operations[0].Table);
        Assert.Equal("admin", builder.Operations[1].Schema);
        Assert.Equal("logs", builder.Operations[1].Table);
    }

    [Fact]
    public void AddColumn_WithBasicParameters_AddsAddColumnOperation()
    {
        var builder = new MigrationBuilder();

        builder.AddColumn("public", "users", "name", "varchar", false);

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.AddColumn, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("users", operation.Table);
        Assert.Equal("name", operation.Column);
        Assert.Equal("varchar", operation.SqlType);
        Assert.False(operation.IsNullable);
        Assert.Null(operation.DefaultSql);
        Assert.False(operation.IsPrimaryKey);
        Assert.False(operation.IsIdentity);
    }

    [Fact]
    public void AddColumn_NullableColumn_SetsIsNullableTrue()
    {
        var builder = new MigrationBuilder();

        builder.AddColumn("public", "users", "email", "varchar", true);

        var operation = Assert.Single(builder.Operations);
        Assert.True(operation.IsNullable);
    }

    [Fact]
    public void AddColumn_WithDefaultValue_SetsDefaultSql()
    {
        var builder = new MigrationBuilder();

        builder.AddColumn("public", "users", "status", "varchar", false, "'active'");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal("'active'", operation.DefaultSql);
    }

    [Fact]
    public void AddColumn_WithPrimaryKey_SetsPrimaryKeyFlag()
    {
        var builder = new MigrationBuilder();

        builder.AddColumn("public", "users", "id", "int4", false, isPrimaryKey: true);

        var operation = Assert.Single(builder.Operations);
        Assert.True(operation.IsPrimaryKey);
    }

    [Fact]
    public void AddColumn_WithIdentity_SetsIdentityFlags()
    {
        var builder = new MigrationBuilder();

        builder.AddColumn("public", "users", "id", "int4", false,
            isPrimaryKey: true, isIdentity: true, identityGeneration: "always");

        var operation = Assert.Single(builder.Operations);
        Assert.True(operation.IsIdentity);
        Assert.Equal("always", operation.IdentityGeneration);
    }

    [Fact]
    public void AddColumn_WithAllParameters_SetsAllProperties()
    {
        var builder = new MigrationBuilder();

        builder.AddColumn("custom", "entities", "pk_id", "bigint",
            false, "1", true,
            true, "by default");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.AddColumn, operation.Type);
        Assert.Equal("custom", operation.Schema);
        Assert.Equal("entities", operation.Table);
        Assert.Equal("pk_id", operation.Column);
        Assert.Equal("bigint", operation.SqlType);
        Assert.False(operation.IsNullable);
        Assert.Equal("1", operation.DefaultSql);
        Assert.True(operation.IsPrimaryKey);
        Assert.True(operation.IsIdentity);
        Assert.Equal("by default", operation.IdentityGeneration);
    }

    [Fact]
    public void DropTable_AddsDropTableOperation()
    {
        var builder = new MigrationBuilder();

        builder.DropTable("public", "old_table");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.DropTable, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("old_table", operation.Table);
    }

    [Fact]
    public void DropColumn_AddsDropColumnOperation()
    {
        var builder = new MigrationBuilder();

        builder.DropColumn("public", "users", "deprecated_field");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.DropColumn, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("users", operation.Table);
        Assert.Equal("deprecated_field", operation.Column);
    }

    [Fact]
    public void AlterColumnType_AddsAlterColumnTypeOperation()
    {
        var builder = new MigrationBuilder();

        builder.AlterColumnType("public", "users", "age", "bigint");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.AlterColumnType, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("users", operation.Table);
        Assert.Equal("age", operation.Column);
        Assert.Equal("bigint", operation.SqlType);
    }

    [Fact]
    public void AlterNullability_ToNullable_AddsAlterNullabilityOperation()
    {
        var builder = new MigrationBuilder();

        builder.AlterNullability("public", "users", "phone", true);

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.AlterNullability, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("users", operation.Table);
        Assert.Equal("phone", operation.Column);
        Assert.True(operation.IsNullable);
    }

    [Fact]
    public void AlterNullability_ToNotNullable_SetsIsNullableFalse()
    {
        var builder = new MigrationBuilder();

        builder.AlterNullability("public", "users", "email", false);

        var operation = Assert.Single(builder.Operations);
        Assert.False(operation.IsNullable);
    }

    [Fact]
    public void SetDefault_AddsSetDefaultOperation()
    {
        var builder = new MigrationBuilder();

        builder.SetDefault("public", "users", "created_at", "NOW()");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.SetDefault, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("users", operation.Table);
        Assert.Equal("created_at", operation.Column);
        Assert.Equal("NOW()", operation.DefaultSql);
    }

    [Fact]
    public void DropDefault_AddsDropDefaultOperation()
    {
        var builder = new MigrationBuilder();

        builder.DropDefault("public", "users", "status");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.DropDefault, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("users", operation.Table);
        Assert.Equal("status", operation.Column);
    }

    [Fact]
    public void AddUnique_AddsAddUniqueOperation()
    {
        var builder = new MigrationBuilder();

        builder.AddUnique("public", "users", "email");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.AddUnique, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("users", operation.Table);
        Assert.Equal("email", operation.Column);
    }

    [Fact]
    public void DropConstraint_AddsDropConstraintOperation()
    {
        var builder = new MigrationBuilder();

        builder.DropConstraint("public", "users", "users_email_key");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.DropConstraint, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("users", operation.Table);
        Assert.Equal("users_email_key", operation.ConstraintName);
    }

    [Fact]
    public void AddForeignKey_WithMinimalParameters_AddsAddForeignKeyOperation()
    {
        var builder = new MigrationBuilder();

        builder.AddForeignKey("public", "orders", "user_id", "fk_orders_user",
            "public", "users", "id");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal(MigrationOperationType.AddForeignKey, operation.Type);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("orders", operation.Table);
        Assert.Equal("user_id", operation.Column);
        Assert.Equal("fk_orders_user", operation.ConstraintName);
        Assert.Equal("public", operation.RefSchema);
        Assert.Equal("users", operation.RefTable);
        Assert.Equal("id", operation.RefColumn);
        Assert.Null(operation.OnDeleteRule);
    }

    [Fact]
    public void AddForeignKey_WithOnDeleteCascade_SetsOnDeleteRule()
    {
        var builder = new MigrationBuilder();

        builder.AddForeignKey("public", "orders", "user_id", "fk_orders_user",
            "public", "users", "id", "CASCADE");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal("CASCADE", operation.OnDeleteRule);
    }

    [Fact]
    public void AddForeignKey_WithOnDeleteSetNull_SetsOnDeleteRule()
    {
        var builder = new MigrationBuilder();

        builder.AddForeignKey("public", "orders", "user_id", "fk_orders_user",
            "public", "users", "id", "SET NULL");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal("SET NULL", operation.OnDeleteRule);
    }

    [Fact]
    public void AddForeignKey_CrossSchema_SetsCorrectSchemas()
    {
        var builder = new MigrationBuilder();

        builder.AddForeignKey("custom", "orders", "user_id", "fk_orders_user",
            "public", "users", "id");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal("custom", operation.Schema);
        Assert.Equal("public", operation.RefSchema);
    }

    [Fact]
    public void Operations_InitiallyEmpty_ReturnsEmptyList()
    {
        var builder = new MigrationBuilder();

        Assert.Empty(builder.Operations);
    }

    [Fact]
    public void Operations_AfterMultipleOperations_ReturnsAllInOrder()
    {
        var builder = new MigrationBuilder();

        builder.CreateSchema("public");
        builder.CreateTable("public", "users");
        builder.AddColumn("public", "users", "id", "int4", false, isPrimaryKey: true);
        builder.AddColumn("public", "users", "name", "varchar", false);

        Assert.Equal(4, builder.Operations.Count);
        Assert.Equal(MigrationOperationType.CreateSchema, builder.Operations[0].Type);
        Assert.Equal(MigrationOperationType.CreateTable, builder.Operations[1].Type);
        Assert.Equal(MigrationOperationType.AddColumn, builder.Operations[2].Type);
        Assert.Equal(MigrationOperationType.AddColumn, builder.Operations[3].Type);
    }

    [Fact]
    public void Operations_IsReadOnly_ReturnsReadOnlyCollection()
    {
        var builder = new MigrationBuilder();
        builder.CreateTable("public", "test");

        var operations = builder.Operations;

        Assert.IsAssignableFrom<IReadOnlyList<MigrationOperation>>(operations);
    }

    [Fact]
    public void MixedOperations_ComplexMigration_AddsAllOperationsInCorrectOrder()
    {
        var builder = new MigrationBuilder();

        builder.CreateSchema("app");
        builder.CreateTable("app", "users");
        builder.AddColumn("app", "users", "id", "int4", false, isPrimaryKey: true, isIdentity: true);
        builder.AddColumn("app", "users", "email", "varchar", false);
        builder.AddUnique("app", "users", "email");
        builder.CreateTable("app", "orders");
        builder.AddColumn("app", "orders", "id", "int4", false, isPrimaryKey: true);
        builder.AddColumn("app", "orders", "user_id", "int4", false);
        builder.AddForeignKey("app", "orders", "user_id", "fk_orders_user", "app", "users", "id", "CASCADE");
        builder.DropTable("app", "legacy_table");

        Assert.Equal(10, builder.Operations.Count);
        Assert.Equal(MigrationOperationType.CreateSchema, builder.Operations[0].Type);
        Assert.Equal(MigrationOperationType.CreateTable, builder.Operations[1].Type);
        Assert.Equal(MigrationOperationType.AddColumn, builder.Operations[2].Type);
        Assert.Equal(MigrationOperationType.AddColumn, builder.Operations[3].Type);
        Assert.Equal(MigrationOperationType.AddUnique, builder.Operations[4].Type);
        Assert.Equal(MigrationOperationType.CreateTable, builder.Operations[5].Type);
        Assert.Equal(MigrationOperationType.AddColumn, builder.Operations[6].Type);
        Assert.Equal(MigrationOperationType.AddColumn, builder.Operations[7].Type);
        Assert.Equal(MigrationOperationType.AddForeignKey, builder.Operations[8].Type);
        Assert.Equal(MigrationOperationType.DropTable, builder.Operations[9].Type);
    }

    [Fact]
    public void AlterOperations_SequentialChanges_AddsAllOperations()
    {
        var builder = new MigrationBuilder();

        builder.AlterColumnType("public", "users", "age", "int4");
        builder.AlterNullability("public", "users", "age", true);
        builder.SetDefault("public", "users", "age", "0");

        Assert.Equal(3, builder.Operations.Count);
        Assert.Equal("age", builder.Operations[0].Column);
        Assert.Equal("age", builder.Operations[1].Column);
        Assert.Equal("age", builder.Operations[2].Column);
    }

    [Fact]
    public void ConstraintOperations_AddAndDrop_AddsCorrectOperations()
    {
        var builder = new MigrationBuilder();

        builder.AddUnique("public", "users", "username");
        builder.DropConstraint("public", "users", "old_constraint");

        Assert.Equal(2, builder.Operations.Count);
        Assert.Equal(MigrationOperationType.AddUnique, builder.Operations[0].Type);
        Assert.Equal(MigrationOperationType.DropConstraint, builder.Operations[1].Type);
    }

    [Fact]
    public void DefaultOperations_SetAndDrop_AddsCorrectOperations()
    {
        var builder = new MigrationBuilder();

        builder.SetDefault("public", "products", "price", "0.00");
        builder.DropDefault("public", "products", "discount");

        Assert.Equal(2, builder.Operations.Count);
        Assert.Equal(MigrationOperationType.SetDefault, builder.Operations[0].Type);
        Assert.Equal("0.00", builder.Operations[0].DefaultSql);
        Assert.Equal(MigrationOperationType.DropDefault, builder.Operations[1].Type);
    }

    [Fact]
    public void CreateSchema_WithEmptyString_AddsOperation()
    {
        var builder = new MigrationBuilder();

        builder.CreateSchema("");

        var operation = Assert.Single(builder.Operations);
        Assert.Equal("", operation.Schema);
    }

    [Fact]
    public void AddColumn_WithNullDefaultSql_LeavesDefaultSqlNull()
    {
        var builder = new MigrationBuilder();

        builder.AddColumn("public", "users", "middle_name", "varchar", true, null);

        var operation = Assert.Single(builder.Operations);
        Assert.Null(operation.DefaultSql);
    }

    [Fact]
    public void AddForeignKey_WithNullOnDeleteRule_LeavesOnDeleteRuleNull()
    {
        var builder = new MigrationBuilder();

        builder.AddForeignKey("public", "orders", "user_id", "fk_orders_user",
            "public", "users", "id", null);

        var operation = Assert.Single(builder.Operations);
        Assert.Null(operation.OnDeleteRule);
    }
}