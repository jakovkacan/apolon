using System.Collections;
using System.Data.Common;
using Apolon.Core.Exceptions;
using Npgsql;

namespace Apolon.Core.DataAccess;

internal class DbConnectionNpgsql(string connectionString) : IDbConnection
{
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;
    public string ConnectionString { get; } = connectionString;
    public System.Data.ConnectionState State => _connection?.State ?? System.Data.ConnectionState.Closed;

    public void OpenConnection()
    {
        _connection = new NpgsqlConnection(ConnectionString);
        _connection.Open();
    }

    public Task OpenConnectionAsync()
    {
        _connection = new NpgsqlConnection(ConnectionString);
        return _connection.OpenAsync();
    }

    public void CloseConnection()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    public ValueTask? CloseConnectionAsync()
    {
        _connection?.CloseAsync();
        return _connection?.DisposeAsync();
    }

    public DbCommand CreateCommand(string sql)
    {
        if (_connection?.State != System.Data.ConnectionState.Open)
            OpenConnection();

        var command = _connection?.CreateCommand();
        
        if (command == null) throw new OrmException("Could not create command");
        
        command.CommandText = sql;
        command.Transaction = _transaction;
            
        return command;
    }
    
    public void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = (NpgsqlParameter)command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;

        if (value is IEnumerable and not string)
        {
            // Only set NpgsqlDbType if it's an array/collection
            var elementType = GetNpgsqlDbType(value);
            parameter.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Array | elementType;
        }
        // For non-enumerables, let Npgsql infer the type naturally from parameter.Value

        command.Parameters.Add(parameter);
    }
    
    private static NpgsqlTypes.NpgsqlDbType GetNpgsqlDbType(object value)
    {
        var type = value.GetType();
        Type? elementType = null;

        if (type.IsArray)
        {
            elementType = type.GetElementType();
        }
        else if (type.IsGenericType)
        {
            elementType = type.GetGenericArguments().FirstOrDefault();
        }
        
        // If the type is 'object', peek at the first element to find the real type
        if (elementType == null || elementType == typeof(object))
        {
            if (value is IEnumerable enumerable)
            {
                var firstItem = enumerable.Cast<object>().FirstOrDefault(x => x != null);
                if (firstItem != null)
                {
                    elementType = firstItem.GetType();
                }
            }
        }

        if (elementType == null) return NpgsqlTypes.NpgsqlDbType.Text;

        // Handle Nullable types by getting the underlying type
        var underlyingType = Nullable.GetUnderlyingType(elementType) ?? elementType;

        if (underlyingType == typeof(int)) return NpgsqlTypes.NpgsqlDbType.Integer;
        if (underlyingType == typeof(long)) return NpgsqlTypes.NpgsqlDbType.Bigint;
        if (underlyingType == typeof(string)) return NpgsqlTypes.NpgsqlDbType.Text;
        if (underlyingType == typeof(Guid)) return NpgsqlTypes.NpgsqlDbType.Uuid;
        if (underlyingType == typeof(DateTime)) return NpgsqlTypes.NpgsqlDbType.Timestamp;
        if (underlyingType == typeof(bool)) return NpgsqlTypes.NpgsqlDbType.Boolean;
        if (underlyingType == typeof(double) || underlyingType == typeof(float)) return NpgsqlTypes.NpgsqlDbType.Double;
        if (underlyingType == typeof(decimal)) return NpgsqlTypes.NpgsqlDbType.Numeric;
        
        return NpgsqlTypes.NpgsqlDbType.Text;
    }

    public DbDataReader ExecuteReader(DbCommand command)
    {
        return command.ExecuteReader();
    }

    public int ExecuteNonQuery(DbCommand command)
    {
        return command.ExecuteNonQuery();
    }

    public object? ExecuteScalar(DbCommand command)
    {
        return command.ExecuteScalar();
    }

    public void BeginTransaction()
    {
        if (_connection?.State != System.Data.ConnectionState.Open)
            OpenConnection();

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