using System.Data.Common;
using System.Reflection;
using Apolon.Core.Attributes;
using Apolon.Core.Exceptions;
using Apolon.Core.Mapping.Models;

namespace Apolon.Core.Mapping;

internal static class EntityMapper
{
    private static readonly Dictionary<Type, EntityMetadata> MetadataCache = new();

    public static EntityMetadata GetMetadata(Type entityType)
    {
        if (MetadataCache.TryGetValue(entityType, out var cached))
            return cached;

        var tableAttr = entityType.GetCustomAttribute<TableAttribute>()
                        ?? throw new MappingException(
                            $"Type {entityType.Name} must be annotated with [Table] attribute");

        var metadata = new EntityMetadata
        {
            EntityType = entityType,
            TableName = tableAttr.Name,
            Schema = tableAttr.Schema,
            Columns = ExtractColumns(entityType),
            PrimaryKey = ExtractPrimaryKey(entityType),
            ForeignKeys = ExtractForeignKeys(entityType),
            Relationships = ExtractRelationships(entityType)
        };

        MetadataCache[entityType] = metadata;
        return metadata;
    }

    private static List<PropertyMetadata> ExtractColumns(Type entityType)
    {
        var columns = new List<PropertyMetadata>();
        var nullabilityContext = new NullabilityInfoContext();

        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<NotMappedAttribute>() != null)
                continue;

            // skip navigation properties 
            if (prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
                continue;

            if (!MapperUtils.IsPrimitiveOrSimpleType(prop.PropertyType))
                continue;

            var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var isRequiredAttrPresent = prop.GetCustomAttribute<RequiredAttribute>() != null;
            var isOptionalAttrPresent = prop.GetCustomAttribute<OptionalAttribute>() != null;
            var isUniqueAttrPresent = prop.GetCustomAttribute<UniqueAttribute>() != null;

            var columnName = columnAttr?.Name ?? MapperUtils.ConvertPascalToSnakeCase(prop.Name);
            var dbType = columnAttr?.DbType ?? TypeMapper.GetPostgresType(prop.PropertyType);

            // 1) [Required] => NOT NULL
            // 2) [Optional] => NULL
            // 3) infer from nullable reference types
            var inferredNullable = IsPropertyNullable(nullabilityContext, prop);
            var isNullable = !isRequiredAttrPresent && (isOptionalAttrPresent || inferredNullable);

            var defaultValue = columnAttr?.DefaultValue;
            var defaultIsRawSql = columnAttr?.DefaultIsRawSql ?? false;

            columns.Add(new PropertyMetadata
            {
                PropertyName = prop.Name,
                ColumnName = columnName,
                DbType = dbType,
                IsNullable = isNullable,
                DefaultValue = defaultValue,
                DefaultIsRawSql = defaultIsRawSql,
                IsUnique = isUniqueAttrPresent,
                Property = prop
            });
        }

        return columns;
    }

    private static PrimaryKeyMetadata ExtractPrimaryKey(Type entityType)
    {
        var prop = entityType.GetProperties().FirstOrDefault(p =>
            p.GetCustomAttribute<PrimaryKeyAttribute>() != null);

        if (prop == null)
            throw new MappingException($"Type {entityType.Name} must have a [PrimaryKey] property");

        var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
        return new PrimaryKeyMetadata
        {
            PropertyName = prop.Name,
            ColumnName = columnAttr?.Name ?? MapperUtils.ConvertPascalToSnakeCase(prop.Name),
            AutoIncrement = prop.GetCustomAttribute<PrimaryKeyAttribute>()?.AutoIncrement ?? true,
            Property = prop
        };
    }

    private static List<ForeignKeyMetadata> ExtractForeignKeys(Type entityType)
    {
        var fks = new List<ForeignKeyMetadata>();

        foreach (var prop in entityType.GetProperties())
        {
            var fkAttr = prop.GetCustomAttribute<ForeignKeyAttribute>();
            if (fkAttr == null) continue;

            var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();

            var referencedColumn = fkAttr.ReferencedColumn ?? GetMetadata(fkAttr.ReferencedTable).PrimaryKey.ColumnName;

            fks.Add(new ForeignKeyMetadata
            {
                PropertyName = prop.Name,
                ColumnName = columnAttr?.Name ?? MapperUtils.ConvertPascalToSnakeCase(prop.Name),
                ReferencedTable = fkAttr.ReferencedTable,
                ReferencedColumn = referencedColumn,
                OnDeleteBehavior = fkAttr.OnDeleteBehavior,
                Property = prop
            });
        }

        return fks;
    }

    private static List<RelationshipMetadata> ExtractRelationships(Type entityType)
    {
        var relationships = new List<RelationshipMetadata>();

        foreach (var prop in entityType.GetProperties())
            // 1-to-Many: ICollection<T>
            if (prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
            {
                var relatedType = prop.PropertyType.GetGenericArguments()[0];
                relationships.Add(new RelationshipMetadata
                {
                    PropertyName = prop.Name,
                    RelatedType = relatedType,
                    Cardinality = RelationshipCardinality.OneToMany,
                    Property = prop
                });
            }
            // Many-to-1 or 1-to-1: direct reference
            else if (MapperUtils.IsPersistentType(prop.PropertyType))
            {
                // Determine if FK property exists with matching name
                var fkProp = entityType.GetProperty(prop.Name + "Id");
                if (fkProp?.GetCustomAttribute<ForeignKeyAttribute>() != null)
                    relationships.Add(new RelationshipMetadata
                    {
                        PropertyName = prop.Name,
                        RelatedType = prop.PropertyType,
                        Cardinality = RelationshipCardinality.ManyToOne,
                        ForeignKeyProperty = fkProp.Name,
                        Property = prop
                    });
            }

        return relationships;
    }

    private static bool IsPropertyNullable(NullabilityInfoContext context, PropertyInfo prop)
    {
        // value types
        if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
            return true;

        // reference types
        if (!prop.PropertyType.IsValueType)
        {
            var nullabilityInfo = context.Create(prop);
            return nullabilityInfo.WriteState == NullabilityState.Nullable;
        }

        // non-nullable
        return false;
    }

    public static T MapEntity<T>(DbDataReader reader, EntityMetadata metadata) where T : class
    {
        var entity = Activator.CreateInstance<T>();
        foreach (var column in metadata.Columns)
        {
            var ordinal = reader.GetOrdinal(column.ColumnName);
            var value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            column.Property.SetValue(entity, TypeMapper.ConvertFromDb(value, column.Property.PropertyType));
        }

        return entity;
    }

    public static object MapEntity(DbDataReader reader, EntityMetadata metadata)
    {
        var entity = Activator.CreateInstance(metadata.EntityType)
                     ?? throw new InvalidOperationException($"Could not create instance of {metadata.EntityType.Name}");

        foreach (var column in metadata.Columns)
        {
            var ordinal = reader.GetOrdinal(column.ColumnName);
            var value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            var convertedValue = TypeMapper.ConvertFromDb(value, column.Property.PropertyType);
            column.Property.SetValue(entity, convertedValue);
        }

        return entity;
    }
}