using Castle.DynamicProxy;
using System.Collections.Concurrent;

namespace Apolon.Core.Proxies;

/// <summary>
/// Factory for creating Castle.Core dynamic proxies with lazy loading support.
/// </summary>
internal static class LazyLoadingProxyFactory
{
    private static readonly ProxyGenerator ProxyGenerator = new();
    private static readonly ConcurrentDictionary<Type, ProxyGenerationOptions> CachedOptions = new();

    /// <summary>
    /// Create a proxy instance of the specified entity type with lazy loading enabled.
    /// </summary>
    public static object CreateProxy(Type entityType, ILazyLoader lazyLoader, NavigationLoadState navigationLoadState)
    {
        // Validate that entity has a parameterless constructor
        var constructor = entityType.GetConstructor(Type.EmptyTypes);
        if (constructor == null)
        {
            throw new InvalidOperationException(
                $"Entity type '{entityType.Name}' must have a parameterless constructor for lazy loading proxies to work.");
        }

        // Create the interceptor
        var interceptor = new LazyLoadingInterceptor(lazyLoader, navigationLoadState, entityType);

        // Get or create proxy generation options (cached for performance)
        var options = CachedOptions.GetOrAdd(entityType, _ => new ProxyGenerationOptions());

        // Create the proxy
        var proxy = ProxyGenerator.CreateClassProxy(entityType, options, interceptor);


        return proxy;
    }

    /// <summary>
    /// Create a typed proxy instance.
    /// </summary>
    public static T CreateProxy<T>(ILazyLoader lazyLoader, NavigationLoadState navigationLoadState) where T : class
    {
        return (T)CreateProxy(typeof(T), lazyLoader, navigationLoadState);
    }

    /// <summary>
    /// Check if a type is a proxy created by Castle.Core.
    /// </summary>
    public static bool IsProxy(object? entity)
    {
        if (entity == null)
            return false;

        var type = entity.GetType();
        return type.FullName != null && type.FullName.Contains("Proxy");
    }

    /// <summary>
    /// Get the base entity type from a proxy type.
    /// </summary>
    public static Type GetBaseType(Type proxyType)
    {
        if (proxyType.FullName != null && proxyType.FullName.Contains("Proxy"))
        {
            return proxyType.BaseType ?? proxyType;
        }
        return proxyType;
    }
}


