using System.Collections;
using System.Data;
using System.Data.Common;
using Apolon.Core.Exceptions;
using Apolon.Core.Sql;
using Npgsql;
using NpgsqlTypes;

namespace Apolon.Core.DataAccess;

internal class DbConnectionNpgsql(string connectionString) : IDbConnection, IAsyncDisposable
{
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;
    private bool CanConnect => _connection?.State == ConnectionState.Open;

    public ValueTask DisposeAsync()
    {
        _transaction?.DisposeAsync();
        return _connection?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public string ConnectionString { get; } = connectionString;
    public ConnectionState State => _connection?.State ?? ConnectionState.Closed;

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
        if (!CanConnect)
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
        parameter.Value = value;

        if (value is IEnumerable and not string)
        {
            // Only set NpgsqlDbType if it's an array/collection
            var elementType = GetNpgsqlDbType(value);
            // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
            parameter.NpgsqlDbType = NpgsqlDbType.Array | elementType;
        }
        // For non-enumerables, let Npgsql infer the type naturally from parameter.Value

        command.Parameters.Add(parameter);
    }

    public DbCommand CreateCommandWithParameters(string sql, List<ParameterMapping> parameters)
    {
        var command = CreateCommand(sql);
        foreach (var param in parameters) AddParameter(command, param.Name, param.Value);

        return command;
    }

    public DbDataReader ExecuteReader(DbCommand command)
    {
        return command.ExecuteReader();
    }

    public Task<DbDataReader> ExecuteReaderAsync(DbCommand command)
    {
        return command.ExecuteReaderAsync();
    }

    public int ExecuteNonQuery(DbCommand command)
    {
        return command.ExecuteNonQuery();
    }

    public Task<int> ExecuteNonQueryAsync(DbCommand command)
    {
        return command.ExecuteNonQueryAsync();
    }

    public object? ExecuteScalar(DbCommand command)
    {
        return command.ExecuteScalar();
    }

    public Task<object?> ExecuteScalarAsync(DbCommand command)
    {
        return command.ExecuteScalarAsync();
    }

    public void BeginTransaction()
    {
        if (!CanConnect)
            OpenConnection();

        _transaction = _connection?.BeginTransaction();
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (!CanConnect)
            await OpenConnectionAsync();

        _transaction = await _connection!.BeginTransactionAsync(ct);
    }

    public void CommitTransaction()
    {
        _transaction?.Commit();
        _transaction?.Dispose();
        _transaction = null;
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction == null) return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void RollbackTransaction()
    {
        _transaction?.Rollback();
        _transaction?.Dispose();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction == null) return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
    }

    private static NpgsqlDbType GetNpgsqlDbType(object value)
    {
        var type = value.GetType();
        Type? elementType = null;

        if (type.IsArray)
            elementType = type.GetElementType();
        else if (type.IsGenericType) elementType = type.GetGenericArguments().FirstOrDefault();

        // If the type is 'object', peek at the first element to find the real type
        if (elementType == null || elementType == typeof(object))
            if (value is IEnumerable enumerable)
            {
                var firstItem = enumerable.Cast<object>().FirstOrDefault(x => x != null);
                if (firstItem != null) elementType = firstItem.GetType();
            }

        if (elementType == null) return NpgsqlDbType.Text;

        // Handle Nullable types by getting the underlying type
        var underlyingType = Nullable.GetUnderlyingType(elementType) ?? elementType;

        if (underlyingType == typeof(int)) return NpgsqlDbType.Integer;
        if (underlyingType == typeof(long)) return NpgsqlDbType.Bigint;
        if (underlyingType == typeof(string)) return NpgsqlDbType.Text;
        if (underlyingType == typeof(Guid)) return NpgsqlDbType.Uuid;
        if (underlyingType == typeof(DateTime)) return NpgsqlDbType.Timestamp;
        if (underlyingType == typeof(bool)) return NpgsqlDbType.Boolean;
        if (underlyingType == typeof(double) || underlyingType == typeof(float)) return NpgsqlDbType.Double;
        if (underlyingType == typeof(decimal)) return NpgsqlDbType.Numeric;

        return NpgsqlDbType.Text;
    }
}