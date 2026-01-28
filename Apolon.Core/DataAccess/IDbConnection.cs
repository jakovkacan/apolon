using System.Data.Common;
using Apolon.Core.Sql;

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
    public DbCommand CreateCommandWithParameters(string sql, List<ParameterMapping> parameters);

    public DbDataReader ExecuteReader(DbCommand command);
    public Task<DbDataReader> ExecuteReaderAsync(DbCommand command);

    public int ExecuteNonQuery(DbCommand command);
    public Task<int> ExecuteNonQueryAsync(DbCommand command);

    public object? ExecuteScalar(DbCommand command);
    public Task<object?> ExecuteScalarAsync(DbCommand command);

    public void BeginTransaction();
    public Task BeginTransactionAsync(CancellationToken ct = default);

    public void CommitTransaction();
    public Task CommitTransactionAsync(CancellationToken ct = default);

    public void RollbackTransaction();
    public Task RollbackTransactionAsync(CancellationToken ct = default);
}