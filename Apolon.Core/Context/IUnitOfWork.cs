namespace Apolon.Core.Context;

public interface IUnitOfWork : IDisposable
{
    // Transaction Management
    void BeginTransaction();
    void CommitTransaction();
    void RollbackTransaction();
    
    // Persistence
    int SaveChanges();
    Task<int> SaveChangesAsync();
}