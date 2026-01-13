using System.Reflection;
using Apolon.Core.Attributes;
using Apolon.Core.Exceptions;

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

    private static List<Metadata> ExtractColumns(Type entityType)
    {
        var columns = new List<Metadata>();

        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // skip navigation properties 
            if (prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
                continue;

            if (!MapperUtils.IsPrimitiveOrSimpleType(prop.PropertyType))
                continue;

            var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var columnName = columnAttr?.Name ?? MapperUtils.ConvertPascalToSnakeCase(prop.Name);
            var dbType = columnAttr?.DbType ?? TypeMapper.GetPostgresType(prop.PropertyType);
            var isNullable = columnAttr?.IsNullable ?? 
                             (!prop.PropertyType.IsValueType ||
                              Nullable.GetUnderlyingType(prop.PropertyType) != null);
            var defaultValue = columnAttr?.DefaultValue;
            var isUnique = columnAttr?.IsUnique ?? false;

            columns.Add(new Metadata
            {
                PropertyName = prop.Name,
                ColumnName = columnName,
                DbType = dbType,
                IsNullable = isNullable,
                DefaultValue = defaultValue,
                IsUnique = isUnique,
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
            ColumnName = columnAttr?.Name ?? prop.Name,
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
            fks.Add(new ForeignKeyMetadata
            {
                PropertyName = prop.Name,
                ColumnName = columnAttr?.Name ?? prop.Name,
                ReferencedTable = fkAttr.ReferencedTable,
                ReferencedColumn = fkAttr.ReferencedColumn,
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
        {
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
                {
                    relationships.Add(new RelationshipMetadata
                    {
                        PropertyName = prop.Name,
                        RelatedType = prop.PropertyType,
                        Cardinality = RelationshipCardinality.ManyToOne,
                        ForeignKeyProperty = fkProp.Name,
                        Property = prop
                    });
                }
            }
        }

        return relationships;
    }
}