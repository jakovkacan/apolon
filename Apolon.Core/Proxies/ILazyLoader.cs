namespace Apolon.Core.Proxies;

/// <summary>
/// Service for loading navigation properties on demand.
/// </summary>
public interface ILazyLoader
{
    /// <summary>
    /// Loads a navigation property for the specified entity.
    /// </summary>
    /// <param name="entity">The entity whose navigation property should be loaded</param>
    /// <param name="navigationPropertyName">The name of the navigation property to load</param>
    void Load(object entity, string navigationPropertyName);
    
    /// <summary>
    /// Asynchronously loads a navigation property for the specified entity.
    /// </summary>
    /// <param name="entity">The entity whose navigation property should be loaded</param>
    /// <param name="navigationPropertyName">The name of the navigation property to load</param>
    Task LoadAsync(object entity, string navigationPropertyName);
}