using Castle.DynamicProxy;
using System.Collections;
using Apolon.Core.Mapping;

namespace Apolon.Core.Proxies;

/// <summary>
/// Castle.Core interceptor that implements lazy loading for virtual navigation properties.
/// </summary>
internal class LazyLoadingInterceptor : IInterceptor
{
    private readonly ILazyLoader _lazyLoader;
    private readonly HashSet<string> _navigationPropertyNames;
    private readonly NavigationLoadState _navigationLoadState;

    public LazyLoadingInterceptor(ILazyLoader lazyLoader, NavigationLoadState navigationLoadState, Type entityType)
    {
        _lazyLoader = lazyLoader;
        _navigationLoadState = navigationLoadState;
        
        // Pre-compute navigation property names for performance
        var metadata = EntityMapper.GetMetadata(entityType);
        _navigationPropertyNames = metadata.Relationships.Select(r => r.PropertyName).ToHashSet();
    }

    public void Intercept(IInvocation invocation)
    {
        // Only intercept property getters
        if (!invocation.Method.IsSpecialName || !invocation.Method.Name.StartsWith("get_"))
        {
            invocation.Proceed();
            return;
        }

        // Extract property name from getter method (remove "get_" prefix)
        var propertyName = invocation.Method.Name.Substring(4);

        // Check if this is a navigation property
        if (!_navigationPropertyNames.Contains(propertyName))
        {
            invocation.Proceed();
            return;
        }

        // Check if already loaded
        if (_navigationLoadState.IsNavigationLoaded(invocation.Proxy, propertyName))
        {
            invocation.Proceed();
            return;
        }

        // Proceed to get the current value
        invocation.Proceed();

        // Check if we need to load
        bool shouldLoad = false;
        
        if (invocation.ReturnValue == null)
        {
            shouldLoad = true;
        }
        else if (invocation.ReturnValue is ICollection collection && collection.Count == 0)
        {
            // For collections, load if empty and not yet loaded
            shouldLoad = true;
        }

        if (shouldLoad)
        {
            _lazyLoader.Load(invocation.Proxy, propertyName);
            
            // Get the value again after loading
            invocation.Proceed();
        }
    }
}

