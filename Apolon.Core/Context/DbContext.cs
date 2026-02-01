using System.Reflection;
using Apolon.Core.DataAccess;
using Apolon.Core.DbSet;

namespace Apolon.Core.Context;

public abstract class DbContext : IDisposable
{
    private readonly DbConnectionNpgsql _connection;
    private readonly Dictionary<Type, object> _dbSets = new();
    private DatabaseFacade? _database;
    private bool _disposed;


    protected DbContext(string connectionString)
    {
        _connection = new DbConnectionNpgsql(connectionString);
        _connection.OpenConnection();
        _disposed = false;
    }

    public virtual DatabaseFacade Database
    {
        get
        {
            CheckDisposed();
            return _database ?? throw new InvalidOperationException("Database not initialized. Use CreateAsync.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected static T Create<T>(string connectionString) where T : DbContext
    {
        var context = (T)Activator.CreateInstance(typeof(T),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null, [connectionString], null)!;
        context._database = new DatabaseFacade(context._connection);
        return context;
    }

    // Generic DbSet accessor
    protected DbSet<T> Set<T>() where T : class
    {
        var type = typeof(T);
        if (_dbSets.TryGetValue(type, out var value)) return (DbSet<T>)value;

        value = new DbSet<T>(_connection);
        _dbSets[type] = value;
        return (DbSet<T>)value;
    }

    public virtual int SaveChanges()
    {
        var total = 0;
        foreach (var dbSet in _dbSets.Values)
        {
            // Reflection to call SaveChanges on each DbSet
            var saveMethod = dbSet.GetType().GetMethod("SaveChanges");
            try
            {
                total += (int)(saveMethod?.Invoke(dbSet, null) ?? throw new InvalidOperationException());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        return total;
    }

    public virtual Task<int> SaveChangesAsync()
    {
        return Task.FromResult(SaveChanges());
    }

    private void CheckDisposed()
    {
        if (!_disposed) return;
        throw new ObjectDisposedException(nameof(DbContext));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;
        await _connection.DisposeAsync();
    }
}