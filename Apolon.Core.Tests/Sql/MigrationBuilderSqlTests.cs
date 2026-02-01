using Apolon.Core.Attributes;
using Apolon.Core.Sql;

namespace Apolon.Core.Tests.Sql;

public class MigrationBuilderSqlTests
{
    [Fact]
    public void BuildCreateSchema_WithSimpleSchemaName_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateSchema("custom_schema");

        Assert.Equal("CREATE SCHEMA IF NOT EXISTS custom_schema;", sql);
    }

    [Fact]
    public void BuildCreateSchema_WithPublicSchema_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateSchema("public");

        Assert.Equal("CREATE SCHEMA IF NOT EXISTS public;", sql);
    }

    [Fact]
    public void BuildCreateTable_WithSimpleEntity_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(SimpleTableEntity));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.simple_table (", sql);
        Assert.Contains("id INT PRIMARY KEY GENERATED ALWAYS AS IDENTITY", sql);
        Assert.Contains("name VARCHAR(255) NOT NULL", sql);
        Assert.EndsWith(");", sql);
    }

    [Fact]
    public void BuildCreateTable_WithNonAutoIncrementPrimaryKey_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithNonAutoPk));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.non_auto_pk", sql);
        Assert.Contains("id UUID PRIMARY KEY", sql);
        Assert.DoesNotContain("GENERATED", sql);
        Assert.DoesNotContain("IDENTITY", sql);
    }

    [Fact]
    public void BuildCreateTable_WithNullableColumn_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithNullable));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.nullable_table", sql);
        Assert.Contains("description VARCHAR(255)", sql);
        Assert.DoesNotContain("description VARCHAR(255) NOT NULL", sql);
    }

    [Fact]
    public void BuildCreateTable_WithDefaultValue_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithDefault));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.default_table", sql);
        Assert.Contains("status VARCHAR(255) DEFAULT 'active' NOT NULL", sql);
    }

    [Fact]
    public void BuildCreateTable_WithRawSqlDefault_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithRawDefault));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.raw_default_table", sql);
        Assert.Contains("created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL", sql);
    }

    [Fact]
    public void BuildCreateTable_WithUniqueConstraint_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithUnique));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.unique_table", sql);
        Assert.Contains("email VARCHAR(255) NOT NULL UNIQUE", sql);
    }

    [Fact]
    public void BuildCreateTable_WithForeignKey_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithForeignKey));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.fk_table", sql);
        Assert.Contains("user_id INT NOT NULL", sql);
        Assert.Contains("CONSTRAINT fk_table_user_id_fkey", sql);
        Assert.Contains("FOREIGN KEY (user_id)", sql);
        Assert.Contains("REFERENCES public.user_table(Id)", sql);
        Assert.Contains("ON DELETE NO ACTION", sql);
    }

    [Fact]
    public void BuildCreateTable_WithForeignKeyCascade_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithFkCascade));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.fk_cascade_table", sql);
        Assert.Contains("CONSTRAINT fk_cascade_table_parent_id_fkey", sql);
        Assert.Contains("ON DELETE CASCADE", sql);
    }

    [Fact]
    public void BuildCreateTable_WithForeignKeySetNull_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithFkSetNull));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.fk_setnull_table", sql);
        Assert.Contains("ON DELETE SET NULL", sql);
    }

    [Fact]
    public void BuildCreateTable_WithMultipleForeignKeys_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithMultipleFk));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.multi_fk_table", sql);
        Assert.Contains("CONSTRAINT multi_fk_table_user_id_fkey", sql);
        Assert.Contains("CONSTRAINT multi_fk_table_category_id_fkey", sql);
    }

    [Fact]
    public void BuildCreateTable_WithCustomSchema_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(MigrationCustomSchemaEntity));

        Assert.Contains("CREATE TABLE IF NOT EXISTS custom.custom_table", sql);
        Assert.Contains("id INT PRIMARY KEY GENERATED ALWAYS AS IDENTITY", sql);
    }

    [Fact]
    public void BuildDropTable_WithDefaultCascade_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropTable(typeof(SimpleTableEntity));

        Assert.Equal("DROP TABLE IF EXISTS public.simple_table CASCADE;", sql);
    }

    [Fact]
    public void BuildDropTable_WithoutCascade_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropTable(typeof(SimpleTableEntity), false);

        Assert.Equal("DROP TABLE IF EXISTS public.simple_table;", sql);
    }

    [Fact]
    public void BuildDropTable_WithCustomSchema_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropTable(typeof(MigrationCustomSchemaEntity));

        Assert.Contains("DROP TABLE IF EXISTS custom.custom_table CASCADE;", sql);
    }

    [Fact]
    public void BuildTruncateTable_WithSimpleEntity_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildTruncateTable(typeof(SimpleTableEntity));

        Assert.Equal("TRUNCATE TABLE public.simple_table RESTART IDENTITY CASCADE;", sql);
    }

    [Fact]
    public void BuildTruncateTable_WithCustomSchema_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildTruncateTable(typeof(MigrationCustomSchemaEntity));

        Assert.Equal("TRUNCATE TABLE custom.custom_table RESTART IDENTITY CASCADE;", sql);
    }

    [Fact]
    public void BuildCreateTableFromName_WithSchemaAndTable_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTableFromName("public", "test_table");

        Assert.Equal("CREATE TABLE public.test_table ();", sql);
    }

    [Fact]
    public void BuildCreateTableFromName_WithCustomSchema_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTableFromName("custom_schema", "my_table");

        Assert.Equal("CREATE TABLE custom_schema.my_table ();", sql);
    }

    [Fact]
    public void BuildDropTableFromName_WithDefaultCascade_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropTableFromName("public", "test_table");

        Assert.Equal("DROP TABLE IF EXISTS public.test_table CASCADE;", sql);
    }

    [Fact]
    public void BuildDropTableFromName_WithoutCascade_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropTableFromName("public", "test_table", false);

        Assert.Equal("DROP TABLE IF EXISTS public.test_table;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithBasicColumn_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "name", "TEXT", false, null);

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN name TEXT NOT NULL;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithNullableColumn_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "description", "TEXT", true, null);

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN description TEXT;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithDefaultValue_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "status", "TEXT", false, "'active'");

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN status TEXT DEFAULT 'active' NOT NULL;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithPrimaryKey_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "id", "INTEGER", false, null, true);

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN id INTEGER PRIMARY KEY;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithIdentityAlways_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "id", "INTEGER", false, null,
            isIdentity: true, identityGeneration: "ALWAYS");

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN id INTEGER GENERATED ALWAYS AS IDENTITY;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithIdentityByDefault_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "id", "INTEGER", false, null,
            isIdentity: true, identityGeneration: "BY DEFAULT");

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN id INTEGER GENERATED BY DEFAULT AS IDENTITY;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithIdentityByDefaultVariant_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "id", "INTEGER", false, null,
            isIdentity: true, identityGeneration: "BYDEFAULT");

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN id INTEGER GENERATED BY DEFAULT AS IDENTITY;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithIdentityNoGeneration_DefaultsToAlways()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "id", "INTEGER", false, null,
            isIdentity: true, identityGeneration: null);

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN id INTEGER GENERATED ALWAYS AS IDENTITY;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithIdentityInvalidGeneration_DefaultsToAlways()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "id", "INTEGER", false, null,
            isIdentity: true, identityGeneration: "INVALID");

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN id INTEGER GENERATED ALWAYS AS IDENTITY;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithAllOptions_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "id", "INTEGER", false, "1", true, true,
            "ALWAYS");

        Assert.Equal(
            "ALTER TABLE public.test_table ADD COLUMN id INTEGER DEFAULT 1 PRIMARY KEY GENERATED ALWAYS AS IDENTITY;",
            sql);
    }

    [Fact]
    public void BuildDropColumn_WithSimpleColumn_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropColumn("public", "test_table", "old_column");

        Assert.Equal("ALTER TABLE public.test_table DROP COLUMN IF EXISTS old_column;", sql);
    }

    [Fact]
    public void BuildDropColumn_WithCustomSchema_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropColumn("custom", "my_table", "deprecated");

        Assert.Equal("ALTER TABLE custom.my_table DROP COLUMN IF EXISTS deprecated;", sql);
    }

    [Fact]
    public void BuildAlterColumnType_WithTypeChange_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAlterColumnType("public", "test_table", "age", "BIGINT");

        Assert.Equal("ALTER TABLE public.test_table ALTER COLUMN age TYPE BIGINT;", sql);
    }

    [Fact]
    public void BuildAlterColumnType_WithCustomSchema_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAlterColumnType("custom", "my_table", "price", "NUMERIC(10,2)");

        Assert.Equal("ALTER TABLE custom.my_table ALTER COLUMN price TYPE NUMERIC(10,2);", sql);
    }

    [Fact]
    public void BuildAlterNullability_ToNullable_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAlterNullability("public", "test_table", "description", true);

        Assert.Equal("ALTER TABLE public.test_table ALTER COLUMN description DROP NOT NULL;", sql);
    }

    [Fact]
    public void BuildAlterNullability_ToNotNullable_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAlterNullability("public", "test_table", "email", false);

        Assert.Equal("ALTER TABLE public.test_table ALTER COLUMN email SET NOT NULL;", sql);
    }

    [Fact]
    public void BuildSetDefault_WithStringDefault_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildSetDefault("public", "test_table", "status", "'pending'");

        Assert.Equal("ALTER TABLE public.test_table ALTER COLUMN status SET DEFAULT 'pending';", sql);
    }

    [Fact]
    public void BuildSetDefault_WithRawSqlDefault_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildSetDefault("public", "test_table", "created_at", "CURRENT_TIMESTAMP");

        Assert.Equal("ALTER TABLE public.test_table ALTER COLUMN created_at SET DEFAULT CURRENT_TIMESTAMP;", sql);
    }

    [Fact]
    public void BuildSetDefault_WithNumericDefault_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildSetDefault("public", "test_table", "count", "0");

        Assert.Equal("ALTER TABLE public.test_table ALTER COLUMN count SET DEFAULT 0;", sql);
    }

    [Fact]
    public void BuildDropDefault_WithSimpleColumn_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropDefault("public", "test_table", "status");

        Assert.Equal("ALTER TABLE public.test_table ALTER COLUMN status DROP DEFAULT;", sql);
    }

    [Fact]
    public void BuildDropDefault_WithCustomSchema_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropDefault("custom", "my_table", "created_at");

        Assert.Equal("ALTER TABLE custom.my_table ALTER COLUMN created_at DROP DEFAULT;", sql);
    }

    [Fact]
    public void BuildAddUnique_WithSimpleColumn_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddUnique("public", "test_table", "email");

        Assert.Equal("ALTER TABLE public.test_table ADD CONSTRAINT test_table_email_key UNIQUE (email);", sql);
    }

    [Fact]
    public void BuildAddUnique_WithCustomSchema_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddUnique("custom", "users", "username");

        Assert.Equal("ALTER TABLE custom.users ADD CONSTRAINT users_username_key UNIQUE (username);", sql);
    }

    [Fact]
    public void BuildDropConstraint_WithSimpleConstraint_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropConstraint("public", "test_table", "test_table_email_key");

        Assert.Equal("ALTER TABLE public.test_table DROP CONSTRAINT IF EXISTS test_table_email_key;", sql);
    }

    [Fact]
    public void BuildDropConstraint_WithForeignKeyConstraint_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildDropConstraint("public", "orders", "orders_user_id_fkey");

        Assert.Equal("ALTER TABLE public.orders DROP CONSTRAINT IF EXISTS orders_user_id_fkey;", sql);
    }

    [Fact]
    public void BuildAddForeignKey_WithDefaultOnDelete_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddForeignKey(
            "public", "orders", "user_id", "orders_user_id_fkey",
            "public", "users", "id");

        Assert.Equal(
            "ALTER TABLE public.orders ADD CONSTRAINT orders_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE NO ACTION;",
            sql);
    }

    [Fact]
    public void BuildAddForeignKey_WithCascade_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddForeignKey(
            "public", "order_items", "order_id", "order_items_order_id_fkey",
            "public", "orders", "id", OnDeleteBehavior.Cascade);

        Assert.Equal(
            "ALTER TABLE public.order_items ADD CONSTRAINT order_items_order_id_fkey FOREIGN KEY (order_id) REFERENCES public.orders(id) ON DELETE CASCADE;",
            sql);
    }

    [Fact]
    public void BuildAddForeignKey_WithSetNull_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddForeignKey(
            "public", "posts", "author_id", "posts_author_id_fkey",
            "public", "users", "id", OnDeleteBehavior.SetNull);

        Assert.Equal(
            "ALTER TABLE public.posts ADD CONSTRAINT posts_author_id_fkey FOREIGN KEY (author_id) REFERENCES public.users(id) ON DELETE SET NULL;",
            sql);
    }

    [Fact]
    public void BuildAddForeignKey_WithRestrict_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddForeignKey(
            "public", "invoices", "customer_id", "invoices_customer_id_fkey",
            "public", "customers", "id", OnDeleteBehavior.Restrict);

        Assert.Equal(
            "ALTER TABLE public.invoices ADD CONSTRAINT invoices_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES public.customers(id) ON DELETE RESTRICT;",
            sql);
    }

    [Fact]
    public void BuildAddForeignKey_WithCrossSchema_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildAddForeignKey(
            "sales", "orders", "user_id", "orders_user_id_fkey",
            "auth", "users", "id");

        Assert.Equal(
            "ALTER TABLE sales.orders ADD CONSTRAINT orders_user_id_fkey FOREIGN KEY (user_id) REFERENCES auth.users(id) ON DELETE NO ACTION;",
            sql);
    }

    [Fact]
    public void BuildCreateTable_WithBooleanDefault_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithBooleanDefault));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.bool_default_table", sql);
        Assert.Contains("is_active BOOLEAN DEFAULT true NOT NULL", sql);
    }

    [Fact]
    public void BuildCreateTable_WithDateTimeDefault_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithDateTimeDefault));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.datetime_default_table", sql);
        Assert.Contains("created_at TIMESTAMP DEFAULT '", sql);
    }

    [Fact]
    public void BuildCreateTable_WithNumericDefault_GeneratesCorrectSql()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithNumericDefault));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.numeric_default_table", sql);
        Assert.Contains("count INT DEFAULT 0 NOT NULL", sql);
    }

    [Fact]
    public void BuildCreateTable_WithMultipleColumns_OrdersColumnsCorrectly()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(EntityWithMultipleColumns));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.multi_column_table", sql);
        var idIndex = sql.IndexOf("id INT PRIMARY KEY", StringComparison.Ordinal);
        var nameIndex = sql.IndexOf("name VARCHAR(255) NOT NULL", StringComparison.Ordinal);
        var descIndex = sql.IndexOf("description VARCHAR(255)", StringComparison.Ordinal);
        var statusIndex = sql.IndexOf("status VARCHAR(255) DEFAULT 'active' NOT NULL", StringComparison.Ordinal);

        Assert.True(idIndex < nameIndex);
        Assert.True(nameIndex < descIndex);
        Assert.True(descIndex < statusIndex);
    }

    [Fact]
    public void BuildCreateTable_WithNoForeignKeys_DoesNotIncludeForeignKeyConstraints()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(SimpleTableEntity));

        Assert.DoesNotContain("CONSTRAINT", sql);
        Assert.DoesNotContain("FOREIGN KEY", sql);
        Assert.DoesNotContain("REFERENCES", sql);
    }

    [Fact]
    public void BuildCreateTable_EndsWithSemicolon()
    {
        var sql = MigrationBuilderSql.BuildCreateTable(typeof(SimpleTableEntity));

        Assert.EndsWith(");", sql);
    }

    [Fact]
    public void BuildAddColumn_WithEmptyDefault_UsesNoDefault()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "name", "TEXT", false, "");

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN name TEXT NOT NULL;", sql);
    }

    [Fact]
    public void BuildAddColumn_WithWhitespaceDefault_UsesNoDefault()
    {
        var sql = MigrationBuilderSql.BuildAddColumn("public", "test_table", "name", "TEXT", false, "   ");

        Assert.Equal("ALTER TABLE public.test_table ADD COLUMN name TEXT NOT NULL;", sql);
    }
}

[Table("simple_table", Schema = "public")]
public class SimpleTableEntity
{
    [PrimaryKey] public int Id { get; set; }

    [Required] public string Name { get; set; } = string.Empty;
}

[Table("non_auto_pk", Schema = "public")]
public class EntityWithNonAutoPk
{
    [PrimaryKey(AutoIncrement = false)] public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[Table("nullable_table", Schema = "public")]
public class EntityWithNullable
{
    [PrimaryKey] public int Id { get; set; }

    public string? Description { get; set; }
}

[Table("default_table", Schema = "public")]
public class EntityWithDefault
{
    [PrimaryKey] public int Id { get; set; }

    [Column("status", DefaultValue = "active")]
    [Required]
    public string Status { get; set; } = string.Empty;
}

[Table("raw_default_table", Schema = "public")]
public class EntityWithRawDefault
{
    [PrimaryKey] public int Id { get; set; }

    [Column("created_at", DefaultValue = "CURRENT_TIMESTAMP", DefaultIsRawSql = true)]
    [Required]
    public DateTime CreatedAt { get; set; }
}

[Table("unique_table", Schema = "public")]
public class EntityWithUnique
{
    [PrimaryKey] public int Id { get; set; }

    [Unique]
    [Column("email")]
    [Required]
    public string Email { get; set; } = string.Empty;
}

[Table("user_table", Schema = "public")]
public class UserTableEntity
{
    [PrimaryKey] public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[Table("fk_table", Schema = "public")]
public class EntityWithForeignKey
{
    [PrimaryKey] public int Id { get; set; }

    [ForeignKey(typeof(UserTableEntity), "Id", OnDeleteBehavior = OnDeleteBehavior.NoAction)]
    [Required]
    public int UserId { get; set; }
}

[Table("fk_cascade_table", Schema = "public")]
public class EntityWithFkCascade
{
    [PrimaryKey] public int Id { get; set; }

    [ForeignKey(typeof(UserTableEntity), "Id", OnDeleteBehavior = OnDeleteBehavior.Cascade)]
    [Required]
    public int ParentId { get; set; }
}

[Table("fk_setnull_table", Schema = "public")]
public class EntityWithFkSetNull
{
    [PrimaryKey] public int Id { get; set; }

    [ForeignKey(typeof(UserTableEntity), "Id", OnDeleteBehavior = OnDeleteBehavior.SetNull)]
    public int? ParentId { get; set; }
}

[Table("category_table", Schema = "public")]
public class CategoryTableEntity
{
    [PrimaryKey] public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[Table("multi_fk_table", Schema = "public")]
public class EntityWithMultipleFk
{
    [PrimaryKey] public int Id { get; set; }

    [ForeignKey(typeof(UserTableEntity), "Id")]
    [Required]
    public int UserId { get; set; }

    [ForeignKey(typeof(CategoryTableEntity), "Id")]
    [Required]
    public int CategoryId { get; set; }
}

[Table("custom_table", Schema = "custom")]
public class MigrationCustomSchemaEntity
{
    [PrimaryKey] public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[Table("bool_default_table", Schema = "public")]
public class EntityWithBooleanDefault
{
    [PrimaryKey] public int Id { get; set; }

    [Column("is_active", DefaultValue = true)]
    [Required]
    public bool IsActive { get; set; }
}

[Table("datetime_default_table", Schema = "public")]
public class EntityWithDateTimeDefault
{
    [PrimaryKey] public int Id { get; set; }

    [Column("created_at", DefaultValue = "2026-01-01 00:00:00")]
    [Required]
    public DateTime CreatedAt { get; set; }
}

[Table("numeric_default_table", Schema = "public")]
public class EntityWithNumericDefault
{
    [PrimaryKey] public int Id { get; set; }

    [Column("count", DefaultValue = 0)]
    [Required]
    public int Count { get; set; }
}

[Table("multi_column_table", Schema = "public")]
public class EntityWithMultipleColumns
{
    [PrimaryKey] public int Id { get; set; }

    [Required] public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Column("status", DefaultValue = "active")]
    [Required]
    public string Status { get; set; } = string.Empty;
}