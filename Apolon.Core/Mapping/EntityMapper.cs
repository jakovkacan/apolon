using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Apolon.Core.Attributes;
using Apolon.Core.Exceptions;

namespace Apolon.Core.Mapping;

public class EntityMapper
{
    private static readonly Dictionary<Type, EntityMetadata> MetadataCache = new();

    public static EntityMetadata GetMetadata(Type entityType)
    {
        if (MetadataCache.TryGetValue(entityType, out var cached))
            return cached;

        var tableAttr = entityType.GetCustomAttribute<TableAttribute>()
                        ?? throw new MappingException($"Type {entityType.Name} must be decorated with [Table]");

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
            // Skip navigation properties (ICollection, non-primitive types)
            if (prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
                continue;

            if (!IsPrimitiveOrSimpleType(prop.PropertyType))
                continue;

            var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var columnName = columnAttr?.Name ?? ConvertPascalToSnakeCase(prop.Name);
            var dbType = columnAttr?.DbType ?? TypeMapper.GetPostgresType(prop.PropertyType);
            var isNullable = columnAttr?.IsNullable ?? true;
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
            else if (IsPersistentType(prop.PropertyType))
            {
                // Determine if FK property exists with matching name
                var fkProp = entityType.GetProperty(prop.Name + "Id");
                if (fkProp?.GetCustomAttribute<ForeignKeyAttribute>() != null)
                {
                    relationships.Add(new RelationshipMetadata
                    {
                        PropertyName = prop.Name,
                        RelatedType = prop.PropertyType,
                        Cardinality = RelationshipCardinality.ManyToOne, // or OneToOne
                        ForeignKeyProperty = fkProp.Name,
                        Property = prop
                    });
                }
            }
        }

        return relationships;
    }

    private static bool IsPrimitiveOrSimpleType(Type type)
    {
        return type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) ||
               type == typeof(decimal) || type == typeof(Guid) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
    }

    private static bool IsPersistentType(Type type)
    {
        return type.GetCustomAttribute<TableAttribute>() != null;
    }

    private static string ConvertPascalToSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        var result = new System.Text.StringBuilder();
        result.Append(char.ToLower(pascalCase[0]));

        for (var i = 1; i < pascalCase.Length; i++)
        {
            if (char.IsUpper(pascalCase[i]))
            {
                result.Append('_');
                result.Append(char.ToLower(pascalCase[i]));
            }
            else
            {
                result.Append(pascalCase[i]);
            }
        }

        return result.ToString();
    }
}