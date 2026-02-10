using System.Data.Common;
using System.Reflection;
using Apolon.Core.Attributes;
using Apolon.Core.DataAccess;
using Apolon.Core.Mapping;
using Apolon.Core.Mapping.Models;

namespace Apolon.Core.Proxies;

/// <summary>
/// Implements lazy loading of navigation properties with circular reference detection.
/// </summary>
internal class LazyLoader : ILazyLoader
{
    private readonly IDbConnection _connection;
    private readonly EntityTracker _entityTracker;
    private readonly NavigationLoadState _navigationLoadState;
    
    // Stack to detect circular loading patterns
    [ThreadStatic]
    private static Stack<(Type EntityType, object EntityId, string PropertyName)>? _loadingStack;

    public LazyLoader(IDbConnection connection, EntityTracker entityTracker, NavigationLoadState navigationLoadState)
    {
        _connection = connection;
        _entityTracker = entityTracker;
        _navigationLoadState = navigationLoadState;
    }

    public void Load(object entity, string navigationPropertyName)
    {
        if (entity == null || string.IsNullOrEmpty(navigationPropertyName))
            return;

        // Check if already loaded
        if (_navigationLoadState.IsNavigationLoaded(entity, navigationPropertyName))
            return;

        var entityType = GetBaseType(entity.GetType());
        var metadata = EntityMapper.GetMetadata(entityType);

        // Find the relationship metadata
        var relationship = metadata.Relationships.FirstOrDefault(r => r.PropertyName == navigationPropertyName);
        if (relationship == null)
            return;

        // Get primary key value for circular reference detection
        var primaryKeyValue = metadata.PrimaryKey.Property.GetValue(entity);
        
        // Initialize loading stack if needed
        _loadingStack ??= new Stack<(Type, object, string)>();

        // Check for circular reference
        var loadingKey = (entityType, primaryKeyValue!, navigationPropertyName);
        if (_loadingStack.Contains(loadingKey))
        {
            var cycle = string.Join(" -> ", _loadingStack.Reverse().Select(k => $"{k.EntityType.Name}[{k.EntityId}].{k.PropertyName}"));
            throw new InvalidOperationException($"Circular reference detected: {cycle} -> {entityType.Name}[{primaryKeyValue}].{navigationPropertyName}");
        }

        try
        {
            _loadingStack.Push(loadingKey);

            // Load based on relationship type
            if (relationship.Cardinality == RelationshipCardinality.OneToMany)
            {
                LoadCollection(entity, relationship, metadata);
            }
            else // ManyToOne or OneToOne
            {
                LoadReference(entity, relationship, metadata);
            }

            // Mark as loaded
            _navigationLoadState.MarkNavigationLoaded(entity, navigationPropertyName);
        }
        finally
        {
            _loadingStack.Pop();
        }
    }

    public async Task LoadAsync(object entity, string navigationPropertyName)
    {
        if (entity == null || string.IsNullOrEmpty(navigationPropertyName))
            return;

        // Check if already loaded
        if (_navigationLoadState.IsNavigationLoaded(entity, navigationPropertyName))
            return;

        var entityType = GetBaseType(entity.GetType());
        var metadata = EntityMapper.GetMetadata(entityType);

        // Find the relationship metadata
        var relationship = metadata.Relationships.FirstOrDefault(r => r.PropertyName == navigationPropertyName);
        if (relationship == null)
            return;

        // Get primary key value for circular reference detection
        var primaryKeyValue = metadata.PrimaryKey.Property.GetValue(entity);
        
        // Initialize loading stack if needed
        _loadingStack ??= new Stack<(Type, object, string)>();

        // Check for circular reference
        var loadingKey = (entityType, primaryKeyValue!, navigationPropertyName);
        if (_loadingStack.Contains(loadingKey))
        {
            var cycle = string.Join(" -> ", _loadingStack.Reverse().Select(k => $"{k.EntityType.Name}[{k.EntityId}].{k.PropertyName}"));
            throw new InvalidOperationException($"Circular reference detected: {cycle} -> {entityType.Name}[{primaryKeyValue}].{navigationPropertyName}");
        }

        try
        {
            _loadingStack.Push(loadingKey);

            // Load based on relationship type
            if (relationship.Cardinality == RelationshipCardinality.OneToMany)
            {
                await LoadCollectionAsync(entity, relationship, metadata);
            }
            else // ManyToOne or OneToOne
            {
                await LoadReferenceAsync(entity, relationship, metadata);
            }

            // Mark as loaded
            _navigationLoadState.MarkNavigationLoaded(entity, navigationPropertyName);
        }
        finally
        {
            _loadingStack.Pop();
        }
    }

    private void LoadCollection(object entity, RelationshipMetadata relationship, EntityMetadata entityMetadata)
    {
        var relatedType = relationship.RelatedType;
        var relatedMetadata = EntityMapper.GetMetadata(relatedType);

        // Find foreign key in related entity that points back to this entity
        var foreignKey = relatedMetadata.ForeignKeys
            .FirstOrDefault(fk => fk.ReferencedTable == entityMetadata.EntityType);

        if (foreignKey == null)
            return;
        
        var foreignKeyColumn = relatedMetadata.Columns
            .FirstOrDefault(c => c.PropertyName == foreignKey.PropertyName);
        
        if (foreignKeyColumn == null)
            return;

        // Get this entity's primary key value
        var primaryKeyValue = entityMetadata.PrimaryKey.Property.GetValue(entity);
        if (primaryKeyValue == null)
            return;

        // Build query: SELECT * FROM related_table WHERE foreign_key = @primaryKey
        var sql = $"SELECT {string.Join(", ", relatedMetadata.Columns.Select(c => c.ColumnName))} " +
                  $"FROM {relatedMetadata.Schema}.{relatedMetadata.TableName} " +
                  $"WHERE {foreignKeyColumn.ColumnName} = @fk";

        var command = _connection.CreateCommand(sql);
        _connection.AddParameter(command, "@fk", primaryKeyValue);

        var relatedEntities = new List<object>();

        using (var reader = _connection.ExecuteReader(command))
        {
            while (reader.Read())
            {
                var relatedEntity = MapEntityWithTracking(reader, relatedMetadata);
                relatedEntities.Add(relatedEntity);
            }
        }


        // Create collection and set property
        var collectionType = typeof(List<>).MakeGenericType(relatedType);
        var collection = Activator.CreateInstance(collectionType);

        foreach (var item in relatedEntities)
        {
            collectionType.GetMethod("Add")?.Invoke(collection, [item]);
        }

        relationship.Property.SetValue(entity, collection);
    }

    private async Task LoadCollectionAsync(object entity, RelationshipMetadata relationship, EntityMetadata entityMetadata)
    {
        var relatedType = relationship.RelatedType;
        var relatedMetadata = EntityMapper.GetMetadata(relatedType);

        // Find foreign key in related entity that points back to this entity
        var foreignKey = relatedMetadata.ForeignKeys
            .FirstOrDefault(fk => fk.ReferencedTable == entityMetadata.EntityType);

        if (foreignKey == null)
            return;
        
        var foreignKeyColumn = relatedMetadata.Columns
            .FirstOrDefault(c => c.PropertyName == foreignKey.PropertyName);
        
        if (foreignKeyColumn == null)
            return;

        // Get this entity's primary key value
        var primaryKeyValue = entityMetadata.PrimaryKey.Property.GetValue(entity);
        if (primaryKeyValue == null)
            return;

        // Build query: SELECT * FROM related_table WHERE foreign_key = @primaryKey
        var sql = $"SELECT {string.Join(", ", relatedMetadata.Columns.Select(c => c.ColumnName))} " +
                  $"FROM {relatedMetadata.Schema}.{relatedMetadata.TableName} " +
                  $"WHERE {foreignKeyColumn.ColumnName} = @fk";

        var command = _connection.CreateCommand(sql);
        _connection.AddParameter(command, "@fk", primaryKeyValue);

        var relatedEntities = new List<object>();

        await using (var reader = await _connection.ExecuteReaderAsync(command))
        {
            while (await reader.ReadAsync())
            {
                var relatedEntity = MapEntityWithTracking(reader, relatedMetadata);
                relatedEntities.Add(relatedEntity);
            }
        }

        // Create collection and set property
        var collectionType = typeof(List<>).MakeGenericType(relatedType);
        var collection = Activator.CreateInstance(collectionType);

        foreach (var item in relatedEntities)
        {
            collectionType.GetMethod("Add")?.Invoke(collection, [item]);
        }

        relationship.Property.SetValue(entity, collection);
    }

    private void LoadReference(object entity, RelationshipMetadata relationship, EntityMetadata entityMetadata)
    {
        var relatedType = relationship.RelatedType;
        var relatedMetadata = EntityMapper.GetMetadata(relatedType);

        // Find foreign key in current entity
        var foreignKey = entityMetadata.ForeignKeys
            .FirstOrDefault(fk => fk.ReferencedTable == relatedType);

        if (foreignKey == null)
            return;
        
        var foreignKeyColumn = entityMetadata.Columns
            .FirstOrDefault(c => c.PropertyName == foreignKey.PropertyName);
        
        if (foreignKeyColumn == null)
            return;

        // Get foreign key value
        var foreignKeyValue = foreignKeyColumn.Property.GetValue(entity);
        if (foreignKeyValue == null)
            return;

        // Check if already tracked
        var getTrackedMethod = typeof(EntityTracker).GetMethod("TryGetTracked")!.MakeGenericMethod(relatedType);
        var parameters = new[] { foreignKeyValue, null };
        var isTracked = (bool)getTrackedMethod.Invoke(_entityTracker, parameters)!;

        if (isTracked && parameters[1] != null)
        {
            relationship.Property.SetValue(entity, parameters[1]);
            return;
        }

        // Build query: SELECT * FROM related_table WHERE primary_key = @fk
        var sql = $"SELECT {string.Join(", ", relatedMetadata.Columns.Select(c => c.ColumnName))} " +
                  $"FROM {relatedMetadata.Schema}.{relatedMetadata.TableName} " +
                  $"WHERE {relatedMetadata.PrimaryKey.ColumnName} = @pk";

        var command = _connection.CreateCommand(sql);
        _connection.AddParameter(command, "@pk", foreignKeyValue);

        using var reader = _connection.ExecuteReader(command);
        if (reader.Read())
        {
            var relatedEntity = MapEntityWithTracking(reader, relatedMetadata);
            relationship.Property.SetValue(entity, relatedEntity);
        }
    }

    private async Task LoadReferenceAsync(object entity, RelationshipMetadata relationship, EntityMetadata entityMetadata)
    {
        var relatedType = relationship.RelatedType;
        var relatedMetadata = EntityMapper.GetMetadata(relatedType);

        // Find foreign key in current entity
        var foreignKey = entityMetadata.ForeignKeys
            .FirstOrDefault(fk => fk.ReferencedTable == relatedType);

        if (foreignKey == null)
            return;
        
        var foreignKeyColumn = entityMetadata.Columns
            .FirstOrDefault(c => c.PropertyName == foreignKey.PropertyName);
        
        if (foreignKeyColumn == null)
            return;

        // Get foreign key value
        var foreignKeyValue = foreignKeyColumn.Property.GetValue(entity);
        if (foreignKeyValue == null)
            return;

        // Check if already tracked
        var getTrackedMethod = typeof(EntityTracker).GetMethod("TryGetTracked")!.MakeGenericMethod(relatedType);
        var parameters = new[] { foreignKeyValue, null };
        var isTracked = (bool)getTrackedMethod.Invoke(_entityTracker, parameters)!;

        if (isTracked && parameters[1] != null)
        {
            relationship.Property.SetValue(entity, parameters[1]);
            return;
        }

        // Build query: SELECT * FROM related_table WHERE primary_key = @fk
        var sql = $"SELECT {string.Join(", ", relatedMetadata.Columns.Select(c => c.ColumnName))} " +
                  $"FROM {relatedMetadata.Schema}.{relatedMetadata.TableName} " +
                  $"WHERE {relatedMetadata.PrimaryKey.ColumnName} = @pk";

        var command = _connection.CreateCommand(sql);
        _connection.AddParameter(command, "@pk", foreignKeyValue);

        await using var reader = await _connection.ExecuteReaderAsync(command);
        if (await reader.ReadAsync())
        {
            var relatedEntity = MapEntityWithTracking(reader, relatedMetadata);
            relationship.Property.SetValue(entity, relatedEntity);
        }
    }

    private object MapEntityWithTracking(DbDataReader reader, EntityMetadata metadata)
    {
        // Check if entity is already tracked
        var pkOrdinal = reader.GetOrdinal(metadata.PrimaryKey.ColumnName);
        var pkValue = reader.GetValue(pkOrdinal);

        // Try to get tracked entity
        var trackedMethod = typeof(EntityTracker).GetMethod("TryGetTracked")!.MakeGenericMethod(metadata.EntityType);
        var parameters = new[] { pkValue, null };
        var isTracked = (bool)trackedMethod.Invoke(_entityTracker, parameters)!;

        if (isTracked && parameters[1] != null)
        {
            return parameters[1];
        }

        // Map new entity with lazy loader (enables transitive loading)
        var entity = MapEntityWithLazyLoader(reader, metadata);

        // Track the entity
        _entityTracker.TrackEntity(metadata.EntityType, entity, pkValue);

        return entity;
    }

    private object MapEntityWithLazyLoader(DbDataReader reader, EntityMetadata metadata)
    {
        // Create proxy if navigation properties exist
        var hasNavigationProperties = metadata.Relationships.Count > 0;
        
        object entity;
        if (hasNavigationProperties)
        {
            entity = LazyLoadingProxyFactory.CreateProxy(metadata.EntityType, this, _navigationLoadState);
        }
        else
        {
            entity = Activator.CreateInstance(metadata.EntityType)!;
        }

        // Map scalar properties
        foreach (var column in metadata.Columns)
        {
            var ordinal = reader.GetOrdinal(column.ColumnName);
            var value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            var convertedValue = Mapping.TypeMapper.ConvertFromDb(value, column.Property.PropertyType);
            column.Property.SetValue(entity, convertedValue);
        }

        return entity;
    }

    private static Type GetBaseType(Type type)
    {
        // If it's a Castle proxy, get the base type
        if (type.FullName != null && type.FullName.Contains("Proxy"))
        {
            return type.BaseType ?? type;
        }
        return type;
    }
}

