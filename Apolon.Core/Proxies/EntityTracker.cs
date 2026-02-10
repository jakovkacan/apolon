using System.Collections.Concurrent;

namespace Apolon.Core.Proxies;

/// <summary>
/// Tracks loaded entities to implement identity map pattern and prevent circular reference issues.
/// </summary>
internal class EntityTracker
{
    // Dictionary<EntityType, Dictionary<PrimaryKeyValue, EntityInstance>>
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, object>> _trackedEntities = new();

    /// <summary>
    /// Try to get a tracked entity by its type and primary key.
    /// </summary>
    public bool TryGetTracked<T>(object primaryKeyValue, out T? entity) where T : class
    {
        entity = null;
        
        if (primaryKeyValue == null)
            return false;

        if (_trackedEntities.TryGetValue(typeof(T), out var entityDict))
        {
            if (entityDict.TryGetValue(primaryKeyValue, out var tracked))
            {
                entity = (T)tracked;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Track an entity instance by its type and primary key.
    /// </summary>
    public void TrackEntity<T>(T entity, object primaryKeyValue) where T : class
    {
        if (entity == null || primaryKeyValue == null)
            return;

        var entityDict = _trackedEntities.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<object, object>());
        entityDict[primaryKeyValue] = entity;
    }

    /// <summary>
    /// Track an entity instance (non-generic version).
    /// </summary>
    public void TrackEntity(Type entityType, object entity, object primaryKeyValue)
    {
        if (entity == null || primaryKeyValue == null)
            return;

        var entityDict = _trackedEntities.GetOrAdd(entityType, _ => new ConcurrentDictionary<object, object>());
        entityDict[primaryKeyValue] = entity;
    }

    /// <summary>
    /// Clear all tracked entities.
    /// </summary>
    public void Clear()
    {
        _trackedEntities.Clear();
    }
}

