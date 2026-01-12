using Apolon.Core.Exceptions;
using Npgsql;

namespace Apolon.Core.DataAccess;

public class DbConnection(string connectionString) : IDisposable
{
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public void Open()
    {
        _connection = new NpgsqlConnection(connectionString);
        _connection.Open();
    }

    public void Close()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    public NpgsqlCommand CreateCommand(string sql)
    {
        if (_connection?.State != System.Data.ConnectionState.Open)
            Open();

        var command = _connection?.CreateCommand();
        
        if (command == null) throw new OrmException("Could not create command");
        
        command.CommandText = sql;
        command.Transaction = _transaction;
            
        return command;
    }

    public NpgsqlDataReader ExecuteReader(NpgsqlCommand command)
    {
        return command.ExecuteReader();
    }

    public int ExecuteNonQuery(NpgsqlCommand command)
    {
        return command.ExecuteNonQuery();
    }

    public object ExecuteScalar(NpgsqlCommand command)
    {
        return command.ExecuteScalar() ?? throw new OrmException("Could not execute scalar query");
    }

    public void BeginTransaction()
    {
        if (_connection?.State != System.Data.ConnectionState.Open)
            Open();

        _transaction = _connection?.BeginTransaction();
    }

    public void CommitTransaction()
    {
        _transaction?.Commit();
        _transaction?.Dispose();
        _transaction = null;
    }

    public void RollbackTransaction()
    {
        _transaction?.Rollback();
        _transaction?.Dispose();
        _transaction = null;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
    }
}