using Apolon.Core.DataAccess;
using Apolon.Core.DbSet;

namespace Apolon.Core.Context;

public abstract class DbContext : IDisposable
{
    private readonly DbConnection _connection;
    private readonly Dictionary<Type, object> _dbSets = new();

    protected DbContext(string connectionString)
    {
        _connection = new DbConnection(connectionString);
        _connection.Open();
    }

    // Generic DbSet accessor
    public DbSet<T> Set<T>() where T : class
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
                total += (int)saveMethod.Invoke(dbSet, null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        return total;
    }

    public void BeginTransaction() => _connection.BeginTransaction();
    public void CommitTransaction() => _connection.CommitTransaction();
    public void RollbackTransaction() => _connection.RollbackTransaction();
    
    public void Dispose() => _connection?.Dispose();
}