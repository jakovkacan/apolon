using System.Data.Common;

namespace Apolon.Core.DataAccess;

public interface IDbConnection : IDisposable
{
    public string ConnectionString { get; }
    public System.Data.ConnectionState State { get; }
    
    public void OpenConnection();
    public Task OpenConnectionAsync();

    public void CloseConnection();

    public ValueTask? CloseConnectionAsync();

    public DbCommand CreateCommand(string sql);
    public void AddParameter(DbCommand command, string name, object value);

    public DbDataReader ExecuteReader(DbCommand command);

    public int ExecuteNonQuery(DbCommand command);

    public object ExecuteScalar(DbCommand command);

    public void BeginTransaction();

    public void CommitTransaction();

    public void RollbackTransaction();
}