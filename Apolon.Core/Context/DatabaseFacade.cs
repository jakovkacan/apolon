using System.Data;
using Apolon.Core.Migrations;
using IDbConnection = Apolon.Core.DataAccess.IDbConnection;

namespace Apolon.Core.Context;

public class DatabaseFacade
{
    private readonly IDbConnection _connection;

    internal DatabaseFacade(IDbConnection connection)
    {
        _connection = connection;
    }

    // public static DatabaseFacade Create(IDbConnection connection)
    // {
    //     return new DatabaseFacade(connection);
    // }

    // Connection
    public void OpenConnection()
    {
        _connection.OpenConnection();
    }

    public Task OpenConnectionAsync()
    {
        return _connection.OpenConnectionAsync();
    }

    public void CloseConnection()
    {
        _connection.CloseConnection();
    }

    public string GetConnectionString()
    {
        return _connection.ConnectionString;
    }

    public bool CanConnect()
    {
        return _connection.State == ConnectionState.Open;
    }

    // Transactions
    public void BeginTransaction()
    {
        _connection.BeginTransaction();
    }

    public void CommitTransaction()
    {
        _connection.CommitTransaction();
    }

    public void RollbackTransaction()
    {
        _connection.RollbackTransaction();
    }
}