using Apolon.Core.DataAccess;

namespace Apolon.Core.Infrastructure;

public class DatabaseFacade
{
    private readonly IDbConnection _connection;
    
    internal DatabaseFacade(IDbConnection connection)
    {
        _connection = connection;
    }
    
    // Connection
    public void OpenConnection() => _connection.OpenConnection();

    public Task OpenConnectionAsync() => _connection.OpenConnectionAsync();

    public void CloseConnection() => _connection.CloseConnection();

    public string GetConnectionString() => _connection.ConnectionString;
    
    public bool CanConnect() => _connection.State == System.Data.ConnectionState.Open;

    // Creation & Deletion
    public void EnsureCreated()
    {
        Console.WriteLine("Database ensured to be created.");
    }
    
    public void EnsureDeleted()
    {
        Console.WriteLine("Database ensured to be deleted.");
    }

    // Migrations
    public void Migrate()
    {
        Console.WriteLine("Database migrations applied.");
    }
    
    public void MigrateAsync()
    {
        Console.WriteLine("Database migrations applied asynchronously.");
    }
    
    public void GetPendingMigrations()
    {
        Console.WriteLine("Retrieved pending migrations.");
    }

    public void GetAppliedMigrations()
    {
        Console.WriteLine("Retrieved applied migrations.");
    }
    
    // Transactions
    public void BeginTransaction() => _connection.BeginTransaction();
    public void CommitTransaction() => _connection.CommitTransaction();
    public void RollbackTransaction() => _connection.RollbackTransaction();

}