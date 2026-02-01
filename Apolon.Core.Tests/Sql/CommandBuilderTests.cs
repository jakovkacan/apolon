using Apolon.Core.Attributes;
using Apolon.Core.Sql;

namespace Apolon.Core.Tests.Sql;

public class CommandBuilderTests
{
    [Fact]
    public void BuildInsert_WithSimpleEntity_GeneratesCorrectSql()
    {
        var builder = new CommandBuilder<SimpleEntity>();
        var entity = new SimpleEntity { Name = "John", Age = 30 };

        var (sql, values) = builder.BuildInsert(entity);

        Assert.Contains("INSERT INTO public.simple_entity", sql);
        Assert.Contains("name, age", sql);
        Assert.Contains("@p0, @p1", sql);
        Assert.Equal(2, values.Count);
        Assert.Equal("John", values[0]);
        Assert.Equal(30, values[1]);
    }

    [Fact]
    public void BuildInsert_ExcludesPrimaryKey()
    {
        var builder = new CommandBuilder<SimpleEntity>();
        var entity = new SimpleEntity { Id = 999, Name = "Jane", Age = 25 };

        var (sql, values) = builder.BuildInsert(entity);

        Assert.DoesNotContain("id", sql.ToLower());
        Assert.Equal(2, values.Count);
        Assert.DoesNotContain(999, values);
    }

    [Fact]
    public void BuildInsert_WithNullableProperties_HandlesNullValues()
    {
        var builder = new CommandBuilder<EntityWithNullables>();
        var entity = new EntityWithNullables { Name = "Test", OptionalDescription = null };

        var (sql, values) = builder.BuildInsert(entity);

        Assert.Contains("INSERT INTO public.nullable_entity", sql);
        Assert.Equal(2, values.Count);
        Assert.Equal("Test", values[0]);
        Assert.Null(values[1]);
    }

    [Fact]
    public void BuildInsert_WithMultipleColumns_OrdersParametersCorrectly()
    {
        var builder = new CommandBuilder<ComplexEntity>();
        var entity = new ComplexEntity
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Age = 35
        };

        var (sql, values) = builder.BuildInsert(entity);

        Assert.Contains("@p0, @p1, @p2, @p3", sql);
        Assert.Equal(4, values.Count);
    }

    [Fact]
    public void BuildInsert_WithCustomSchema_UsesSchemaInSql()
    {
        var builder = new CommandBuilder<EntityWithCustomSchema>();
        var entity = new EntityWithCustomSchema { Name = "Test" };

        var (sql, values) = builder.BuildInsert(entity);

        Assert.Contains("custom_schema.custom_table", sql);
    }

    [Fact]
    public void BuildUpdate_WithSimpleEntity_GeneratesCorrectSql()
    {
        var builder = new CommandBuilder<SimpleEntity>();
        var entity = new SimpleEntity { Id = 1, Name = "Updated", Age = 40 };

        var (sql, values, pkValue) = builder.BuildUpdate(entity);

        Assert.Contains("UPDATE public.simple_entity SET", sql);
        Assert.Contains("name = @p0, age = @p1", sql);
        Assert.Contains("WHERE id = @pk", sql);
        Assert.Equal(2, values.Count);
        Assert.Equal("Updated", values[0]);
        Assert.Equal(40, values[1]);
        Assert.Equal(1, pkValue);
    }

    [Fact]
    public void BuildUpdate_ExcludesPrimaryKeyFromSetClause()
    {
        var builder = new CommandBuilder<SimpleEntity>();
        var entity = new SimpleEntity { Id = 5, Name = "Test", Age = 20 };

        var (sql, values, pkValue) = builder.BuildUpdate(entity);

        var setClause = sql.Split("WHERE")[0];
        Assert.DoesNotContain("id =", setClause.ToLower());
        Assert.Equal(2, values.Count);
        Assert.Equal(5, pkValue);
    }

    [Fact]
    public void BuildUpdate_WithNullableProperties_HandlesNullValues()
    {
        var builder = new CommandBuilder<EntityWithNullables>();
        var entity = new EntityWithNullables { Id = 3, Name = "Updated", OptionalDescription = null };

        var (sql, values, pkValue) = builder.BuildUpdate(entity);

        Assert.Contains("UPDATE public.nullable_entity SET", sql);
        Assert.Equal(2, values.Count);
        Assert.Equal("Updated", values[0]);
        Assert.Null(values[1]);
        Assert.Equal(3, pkValue);
    }

    [Fact]
    public void BuildUpdate_WithMultipleColumns_GeneratesCorrectSetClause()
    {
        var builder = new CommandBuilder<ComplexEntity>();
        var entity = new ComplexEntity
        {
            Id = 10,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            Age = 28
        };

        var (sql, values, pkValue) = builder.BuildUpdate(entity);

        Assert.Contains("first_name = @p0", sql);
        Assert.Contains("last_name = @p1", sql);
        Assert.Contains("email = @p2", sql);
        Assert.Contains("age = @p3", sql);
        Assert.Equal(4, values.Count);
        Assert.Equal(10, pkValue);
    }

    [Fact]
    public void BuildUpdate_WithGuidPrimaryKey_ReturnsPrimaryKeyAsGuid()
    {
        var builder = new CommandBuilder<EntityWithGuidPk>();
        var guid = Guid.NewGuid();
        var entity = new EntityWithGuidPk { Id = guid, Name = "Test" };

        var (sql, values, pkValue) = builder.BuildUpdate(entity);

        Assert.IsType<Guid>(pkValue);
        Assert.Equal(guid, pkValue);
    }

    [Fact]
    public void BuildUpdate_WithCustomSchema_UsesSchemaInSql()
    {
        var builder = new CommandBuilder<EntityWithCustomSchema>();
        var entity = new EntityWithCustomSchema { Id = 7, Name = "Updated" };

        var (sql, values, pkValue) = builder.BuildUpdate(entity);

        Assert.Contains("UPDATE custom_schema.custom_table SET", sql);
    }

    [Fact]
    public void BuildDelete_WithSimpleEntity_GeneratesCorrectSql()
    {
        var builder = new CommandBuilder<SimpleEntity>();
        var entity = new SimpleEntity { Id = 42, Name = "ToDelete", Age = 50 };

        var (sql, primaryKey) = builder.BuildDelete(entity);

        Assert.Equal("DELETE FROM public.simple_entity WHERE id = @pk", sql);
        Assert.Equal(42, primaryKey);
    }

    [Fact]
    public void BuildDelete_UsesOnlyPrimaryKey()
    {
        var builder = new CommandBuilder<ComplexEntity>();
        var entity = new ComplexEntity
        {
            Id = 99,
            FirstName = "Any",
            LastName = "Value",
            Email = "any@example.com",
            Age = 30
        };

        var (sql, primaryKey) = builder.BuildDelete(entity);

        Assert.Contains("WHERE id = @pk", sql);
        Assert.Equal(99, primaryKey);
        Assert.DoesNotContain("first_name", sql.ToLower());
        Assert.DoesNotContain("last_name", sql.ToLower());
    }

    [Fact]
    public void BuildDelete_WithGuidPrimaryKey_ReturnsPrimaryKeyAsGuid()
    {
        var builder = new CommandBuilder<EntityWithGuidPk>();
        var guid = Guid.NewGuid();
        var entity = new EntityWithGuidPk { Id = guid, Name = "Delete Me" };

        var (sql, primaryKey) = builder.BuildDelete(entity);

        Assert.IsType<Guid>(primaryKey);
        Assert.Equal(guid, primaryKey);
    }

    [Fact]
    public void BuildDelete_WithCustomSchema_UsesSchemaInSql()
    {
        var builder = new CommandBuilder<EntityWithCustomSchema>();
        var entity = new EntityWithCustomSchema { Id = 15, Name = "Delete" };

        var (sql, primaryKey) = builder.BuildDelete(entity);

        Assert.Contains("DELETE FROM custom_schema.custom_table", sql);
        Assert.Equal(15, primaryKey);
    }

    [Fact]
    public void BuildDelete_WithStringPrimaryKey_ReturnsPrimaryKeyAsString()
    {
        var builder = new CommandBuilder<EntityWithStringPk>();
        var entity = new EntityWithStringPk { Code = "ABC123", Description = "Test" };

        var (sql, primaryKey) = builder.BuildDelete(entity);

        Assert.IsType<string>(primaryKey);
        Assert.Equal("ABC123", primaryKey);
    }

    [Fact]
    public void BuildInsert_WithEntityContainingOnlyPrimaryKey_GeneratesEmptyColumns()
    {
        var builder = new CommandBuilder<EntityWithOnlyPk>();
        var entity = new EntityWithOnlyPk();

        var (sql, values) = builder.BuildInsert(entity);

        Assert.Contains("INSERT INTO public.pk_only_entity", sql);
        Assert.Empty(values);
    }

    [Fact]
    public void BuildUpdate_WithEntityContainingOnlyPrimaryKey_GeneratesEmptySetClause()
    {
        var builder = new CommandBuilder<EntityWithOnlyPk>();
        var entity = new EntityWithOnlyPk { Id = 1 };

        var (sql, values, pkValue) = builder.BuildUpdate(entity);

        Assert.Contains("UPDATE public.pk_only_entity SET", sql);
        Assert.Empty(values);
        Assert.Equal(1, pkValue);
    }
}

[Table("simple_entity", Schema = "public")]
public class SimpleEntity
{
    [PrimaryKey]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }
}

[Table("nullable_entity", Schema = "public")]
public class EntityWithNullables
{
    [PrimaryKey]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? OptionalDescription { get; set; }
}

[Table("complex_entity", Schema = "public")]
public class ComplexEntity
{
    [PrimaryKey]
    public int Id { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int Age { get; set; }
}

[Table("guid_pk_entity", Schema = "public")]
public class EntityWithGuidPk
{
    [PrimaryKey(AutoIncrement = false)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[Table("custom_table", Schema = "custom_schema")]
public class EntityWithCustomSchema
{
    [PrimaryKey]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[Table("string_pk_entity", Schema = "public")]
public class EntityWithStringPk
{
    [PrimaryKey(AutoIncrement = false)]
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

[Table("pk_only_entity", Schema = "public")]
public class EntityWithOnlyPk
{
    [PrimaryKey]
    public int Id { get; set; }
}
