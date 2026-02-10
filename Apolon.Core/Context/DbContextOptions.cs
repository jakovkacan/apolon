namespace Apolon.Core.Context;

/// <summary>
/// Configuration options for DbContext behavior.
/// </summary>
public class DbContextOptions
{
    /// <summary>
    /// Enable lazy loading using dynamic proxies.
    /// Navigation properties must be marked as virtual.
    /// </summary>
    public bool UseLazyLoadingProxies { get; set; }

    /// <summary>
    /// Enable query batching to prevent N+1 queries.
    /// </summary>
    public bool UseIncludeOptimizer { get; set; }

    /// <summary>
    /// Batch threshold for the include optimizer.
    /// </summary>
    public int IncludeOptimizerBatchThreshold { get; set; } = 10;

    /// <summary>
    /// Maximum batch size for bulk loading operations.
    /// </summary>
    public int MaxBatchSize { get; set; } = 1000;
}

/// <summary>
/// Builder for configuring DbContext options with a fluent API.
/// </summary>
public class DbContextOptionsBuilder
{
    private readonly DbContextOptions _options = new();

    /// <summary>
    /// Enable lazy loading using Castle.Core dynamic proxies.
    /// All navigation properties must be marked as virtual.
    /// </summary>
    public DbContextOptionsBuilder UseLazyLoadingProxies()
    {
        _options.UseLazyLoadingProxies = true;
        return this;
    }

    /// <summary>
    /// Enable the include optimizer to batch navigation property loads and prevent N+1 queries.
    /// </summary>
    public DbContextOptionsBuilder UseIncludeOptimizer(int batchThreshold = 10, int maxBatchSize = 1000)
    {
        _options.UseIncludeOptimizer = true;
        _options.IncludeOptimizerBatchThreshold = batchThreshold;
        _options.MaxBatchSize = maxBatchSize;
        return this;
    }

    /// <summary>
    /// Build the options object.
    /// </summary>
    public DbContextOptions Build()
    {
        return _options;
    }
}

