using Apolon.Core.Attributes;
using Apolon.Core.Exceptions;
using Apolon.Core.Mapping;
using Apolon.Core.Mapping.Models;
using Xunit;

namespace Apolon.Core.Tests.Mapping;

public class EntityMapperTests
{
    [Fact]
    public void GetMetadata_WithValidEntity_ReturnsEntityMetadata()
    {
        var metadata = EntityMapper.GetMetadata(typeof(ValidEntity));

        Assert.NotNull(metadata);
        Assert.Equal(typeof(ValidEntity), metadata.EntityType);
        Assert.Equal("valid_entities", metadata.TableName);
        Assert.Equal("public", metadata.Schema);
    }

    [Fact]
    public void GetMetadata_WithoutTableAttribute_ThrowsMappingException()
    {
        var exception = Assert.Throws<MappingException>(() =>
            EntityMapper.GetMetadata(typeof(EntityWithoutTableAttribute)));

        Assert.Contains("must be annotated with [Table] attribute", exception.Message);
    }

    [Fact]
    public void GetMetadata_WithoutPrimaryKey_ThrowsMappingException()
    {
        var exception = Assert.Throws<MappingException>(() =>
            EntityMapper.GetMetadata(typeof(EntityWithoutPrimaryKey)));

        Assert.Contains("must have a [PrimaryKey] property", exception.Message);
    }

    [Fact]
    public void GetMetadata_CachesResult_ReturnsSameInstance()
    {
        var metadata1 = EntityMapper.GetMetadata(typeof(ValidEntity));
        var metadata2 = EntityMapper.GetMetadata(typeof(ValidEntity));

        Assert.Same(metadata1, metadata2);
    }

    [Fact]
    public void GetMetadata_ExtractsColumnsWithCustomNames()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithCustomColumns));

        var column = metadata.Columns.FirstOrDefault(c => c.PropertyName == "CustomName");
        Assert.NotNull(column);
        Assert.Equal("custom_column", column.ColumnName);
    }

    [Fact]
    public void GetMetadata_ExtractsColumnsWithSnakeCaseConversion()
    {
        var metadata = EntityMapper.GetMetadata(typeof(ValidEntity));

        var column = metadata.Columns.FirstOrDefault(c => c.PropertyName == "FirstName");
        Assert.NotNull(column);
        Assert.Equal("first_name", column.ColumnName);
    }

    [Fact]
    public void GetMetadata_SkipsNavigationProperties()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithNavigationProperties));

        Assert.DoesNotContain(metadata.Columns, c => c.PropertyName == "RelatedEntities");
    }

    [Fact]
    public void GetMetadata_ExtractsPrimaryKeyMetadata()
    {
        var metadata = EntityMapper.GetMetadata(typeof(ValidEntity));

        Assert.NotNull(metadata.PrimaryKey);
        Assert.Equal("Id", metadata.PrimaryKey.PropertyName);
        Assert.True(metadata.PrimaryKey.AutoIncrement);
    }

    [Fact]
    public void GetMetadata_ExtractsPrimaryKeyWithCustomColumnName()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithCustomPrimaryKey));

        Assert.Equal("custom_id", metadata.PrimaryKey.ColumnName);
    }

    [Fact]
    public void GetMetadata_ExtractsPrimaryKeyWithAutoIncrementFalse()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithManualPrimaryKey));

        Assert.False(metadata.PrimaryKey.AutoIncrement);
    }

    [Fact]
    public void GetMetadata_ExtractsForeignKeys()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithForeignKey));

        var fk = metadata.ForeignKeys.FirstOrDefault();
        Assert.NotNull(fk);
        Assert.Equal("UserId", fk.PropertyName);
        Assert.Equal(typeof(User), fk.ReferencedTable);
        Assert.Equal("id", fk.ReferencedColumn);
    }

    [Fact]
    public void GetMetadata_InfersReferencedColumnFromReferencedPrimaryKey_WhenNotProvided()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithForeignKeyInferredColumn));

        var fk = Assert.Single(metadata.ForeignKeys);
        Assert.Equal("UserId", fk.PropertyName);
        Assert.Equal(typeof(User), fk.ReferencedTable);

        // User's PK has no [Column], so EntityMapper uses property name ("Id") as PK column name
        Assert.Equal("Id", fk.ReferencedColumn);
    }

    [Fact]
    public void GetMetadata_ExtractsForeignKeyWithOnDeleteBehavior()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithCascadeDelete));

        var fk = metadata.ForeignKeys.FirstOrDefault();
        Assert.NotNull(fk);
        Assert.Equal(OnDeleteBehavior.Cascade, fk.OnDeleteBehavior);
    }

    [Fact]
    public void GetMetadata_ExtractsMultipleForeignKeys()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithMultipleForeignKeys));

        Assert.Equal(2, metadata.ForeignKeys.Count);
        Assert.Contains(metadata.ForeignKeys, x => x.ReferencedTable == typeof(User));
        Assert.Contains(metadata.ForeignKeys, x => x.ReferencedTable == typeof(Company));
    }

    [Fact]
    public void GetMetadata_ExtractsOneToManyRelationships()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithOneToMany));

        var relationship = metadata.Relationships.FirstOrDefault();
        Assert.NotNull(relationship);
        Assert.Equal("Orders", relationship.PropertyName);
        Assert.Equal(RelationshipCardinality.OneToMany, relationship.Cardinality);
    }

    [Fact]
    public void GetMetadata_ExtractsManyToOneRelationships()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithManyToOne));

        var relationship = metadata.Relationships.FirstOrDefault();
        Assert.NotNull(relationship);
        Assert.Equal("User", relationship.PropertyName);
        Assert.Equal(RelationshipCardinality.ManyToOne, relationship.Cardinality);
        Assert.Equal("UserId", relationship.ForeignKeyProperty);
    }

    [Fact]
    public void GetMetadata_DetectsNullableValueTypes()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithNullableProperties));

        var column = metadata.Columns.FirstOrDefault(c => c.PropertyName == "NullableInt");
        Assert.NotNull(column);
        Assert.True(column.IsNullable);
    }

    [Fact]
    public void GetMetadata_DetectsNullableReferenceTypes()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithNullableProperties));

        var column = metadata.Columns.FirstOrDefault(c => c.PropertyName == "NullableString");
        Assert.NotNull(column);
        Assert.True(column.IsNullable);
    }

    [Fact]
    public void GetMetadata_RequiredAttribute_ForcesNotNull()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithRequiredAttribute));

        var requiredRef = metadata.Columns.FirstOrDefault(c => c.PropertyName == "Name");
        Assert.NotNull(requiredRef);
        Assert.False(requiredRef.IsNullable);

        var requiredValue = metadata.Columns.FirstOrDefault(c => c.PropertyName == "OptionalButRequired");
        Assert.NotNull(requiredValue);
        Assert.False(requiredValue.IsNullable);
    }

    [Fact]
    public void GetMetadata_ExtractsColumnDefaultValue()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithDefaultValue));

        var column = metadata.Columns.FirstOrDefault(c => c.PropertyName == "Status");
        Assert.NotNull(column);
        Assert.Equal("active", column.DefaultValue);
    }

    [Fact]
    public void GetMetadata_ExtractsColumnDefaultIsRawSql()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithRawSqlDefault));

        var column = metadata.Columns.FirstOrDefault(c => c.PropertyName == "CreatedAt");
        Assert.NotNull(column);
        Assert.True(column.DefaultIsRawSql);
    }

    [Fact]
    public void GetMetadata_ExtractsUniqueConstraint()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithUniqueColumn));

        var column = metadata.Columns.FirstOrDefault(c => c.PropertyName == "Email");
        Assert.NotNull(column);
        Assert.True(column.IsUnique);
    }

    [Fact]
    public void GetMetadata_ExtractsCustomDbType()
    {
        var metadata = EntityMapper.GetMetadata(typeof(EntityWithCustomDbType));

        var column = metadata.Columns.FirstOrDefault(c => c.PropertyName == "Uuid");
        Assert.NotNull(column);
        Assert.Equal("UUID", column.DbType);
    }
}

// Test models

[Table("users", Schema = "public")]
public class User
{
    [PrimaryKey] public int Id { get; set; }
}

[Table("companies", Schema = "public")]
public class Company
{
    [PrimaryKey] public int Id { get; set; }
}

[Table("valid_entities", Schema = "public")]
public class ValidEntity
{
    [PrimaryKey] public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;
}

public class EntityWithoutTableAttribute
{
    [PrimaryKey] public int Id { get; set; }
}

[Table("no_pk", Schema = "public")]
public class EntityWithoutPrimaryKey
{
    public string Name { get; set; } = string.Empty;
}

[Table("custom_columns", Schema = "public")]
public class EntityWithCustomColumns
{
    [PrimaryKey] public int Id { get; set; }

    [Column("custom_column", DbType = "VARCHAR(255)")]
    public string CustomName { get; set; } = string.Empty;
}

[Table("navigation_props", Schema = "public")]
public class EntityWithNavigationProperties
{
    [PrimaryKey] public int Id { get; set; }

    public ICollection<ValidEntity> RelatedEntities { get; set; } = new List<ValidEntity>();
}

[Table("custom_pk", Schema = "public")]
public class EntityWithCustomPrimaryKey
{
    [PrimaryKey]
    [Column("custom_id", DbType = "INT")]
    public int Id { get; set; }
}

[Table("manual_pk", Schema = "public")]
public class EntityWithManualPrimaryKey
{
    [PrimaryKey(AutoIncrement = false)] public Guid Id { get; set; }
}

[Table("with_fk", Schema = "public")]
public class EntityWithForeignKey
{
    [PrimaryKey] public int Id { get; set; }

    [ForeignKey(typeof(User), "id")] public int UserId { get; set; }
}

[Table("with_fk_inferred_col", Schema = "public")]
public class EntityWithForeignKeyInferredColumn
{
    [PrimaryKey] public int Id { get; set; }

    // No referenced column specified -> EntityMapper should infer from User PK column name
    [ForeignKey(typeof(User))] public int UserId { get; set; }
}

[Table("cascade_delete", Schema = "public")]
public class EntityWithCascadeDelete
{
    [PrimaryKey] public int Id { get; set; }

    [ForeignKey(typeof(User), "id", OnDeleteBehavior = OnDeleteBehavior.Cascade)]
    public int UserId { get; set; }
}

[Table("multiple_fks", Schema = "public")]
public class EntityWithMultipleForeignKeys
{
    [PrimaryKey] public int Id { get; set; }

    [ForeignKey(typeof(User), "id")] public int UserId { get; set; }

    [ForeignKey(typeof(Company), "id")] public int CompanyId { get; set; }
}

[Table("one_to_many", Schema = "public")]
public class EntityWithOneToMany
{
    [PrimaryKey] public int Id { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

[Table("many_to_one", Schema = "public")]
public class EntityWithManyToOne
{
    [PrimaryKey] public int Id { get; set; }

    [ForeignKey(typeof(User), "id")] public int UserId { get; set; }

    public User User { get; set; } = null!;
}

[Table("nullable_props", Schema = "public")]
public class EntityWithNullableProperties
{
    [PrimaryKey] public int Id { get; set; }

    public int? NullableInt { get; set; }

    public string? NullableString { get; set; }
}

[Table("required_props", Schema = "public")]
public class EntityWithRequiredAttribute
{
    [PrimaryKey] public int Id { get; set; }

    [Required] public string? Name { get; set; }

    // Even if the type is nullable, [Required] should force NOT NULL in metadata
    [Required] public int? OptionalButRequired { get; set; }
}

[Table("default_value", Schema = "public")]
public class EntityWithDefaultValue
{
    [PrimaryKey] public int Id { get; set; }

    [Column("status", DefaultValue = "active")]
    public string Status { get; set; } = string.Empty;
}

[Table("raw_sql_default", Schema = "public")]
public class EntityWithRawSqlDefault
{
    [PrimaryKey] public int Id { get; set; }

    [Column("created_at", DefaultValue = "NOW()", DefaultIsRawSql = true)]
    public DateTime CreatedAt { get; set; }
}

[Table("unique_column", Schema = "public")]
public class EntityWithUniqueColumn
{
    [PrimaryKey] public int Id { get; set; }

    [Column("email", IsUnique = true)] public string Email { get; set; } = string.Empty;
}

[Table("custom_db_type", Schema = "public")]
public class EntityWithCustomDbType
{
    [PrimaryKey] public int Id { get; set; }

    [Column("uuid", DbType = "UUID")] public Guid Uuid { get; set; }
}

[Table("orders", Schema = "public")]
public class Order
{
    [PrimaryKey] public int Id { get; set; }
}