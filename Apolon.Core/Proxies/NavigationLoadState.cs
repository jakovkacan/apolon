using System.Runtime.CompilerServices;

namespace Apolon.Core.Proxies;

/// <summary>
/// Tracks which navigation properties have been loaded for each entity to prevent redundant loads and circular references.
/// Uses ConditionalWeakTable for memory-efficient tracking without preventing garbage collection.
/// </summary>
internal class NavigationLoadState
{
    private readonly ConditionalWeakTable<object, HashSet<string>> _loadedProperties = new();

    /// <summary>
    /// Check if a navigation property has been loaded for an entity.
    /// </summary>
    public bool IsNavigationLoaded(object entity, string propertyName)
    {
        if (entity == null || string.IsNullOrEmpty(propertyName))
            return false;

        if (_loadedProperties.TryGetValue(entity, out var properties))
        {
            return properties.Contains(propertyName);
        }

        return false;
    }

    /// <summary>
    /// Mark a navigation property as loaded for an entity.
    /// </summary>
    public void MarkNavigationLoaded(object entity, string propertyName)
    {
        if (entity == null || string.IsNullOrEmpty(propertyName))
            return;

        var properties = _loadedProperties.GetOrCreateValue(entity);
        properties.Add(propertyName);
    }

    /// <summary>
    /// Check if a navigation is currently being loaded (to detect immediate circular references).
    /// </summary>
    public bool IsNavigationLoading(object entity, string propertyName)
    {
        // This will be handled by the loading stack in LazyLoader
        return false;
    }
}

