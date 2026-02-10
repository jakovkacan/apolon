using System.Data.Common;
using Apolon.Core.Exceptions;
using Apolon.Core.Mapping;
using Apolon.Core.Mapping.Models;
using Apolon.Core.Proxies;
using Apolon.Core.Sql;

namespace Apolon.Core.DataAccess;

internal class DatabaseExecutor
{
    private readonly IDbConnection _connection;
    private readonly ILazyLoader? _lazyLoader;
    private readonly EntityTracker? _entityTracker;
    private readonly NavigationLoadState? _navigationLoadState;

    public DatabaseExecutor(IDbConnection connection)
    {
        _connection = connection;
        _lazyLoader = null;
        _entityTracker = null;
        _navigationLoadState = null;
    }

    public DatabaseExecutor(IDbConnection connection, ILazyLoader? lazyLoader, EntityTracker? entityTracker, NavigationLoadState? navigationLoadState)
    {
        _connection = connection;
        _lazyLoader = lazyLoader;
        _entityTracker = entityTracker;
        _navigationLoadState = navigationLoadState;
    }

    public int Insert<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, values) = builder.BuildInsert(entity);
        var command = _connection.CreateCommand(sql);

        for (var i = 0; i < values.Count; i++)
            _connection.AddParameter(command, $"@p{i}", TypeMapper.ConvertToDb(values[i]));

        var generatedId = _connection.ExecuteScalar(command);
    
        // Set the generated ID back on the entity
        var metadata = EntityMapper.GetMetadata(typeof(T));
        metadata.PrimaryKey.Property.SetValue(entity, Convert.ChangeType(generatedId, metadata.PrimaryKey.Property.PropertyType));

        return 1;
    }

    public async Task<int> InsertAsync<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, values) = builder.BuildInsert(entity);
        var command = _connection.CreateCommand(sql);

        for (var i = 0; i < values.Count; i++)
            _connection.AddParameter(command, $"@p{i}", TypeMapper.ConvertToDb(values[i]));
        
        var generatedId = await _connection.ExecuteScalarAsync(command);
    
        // Set the generated ID back on the entity
        var metadata = EntityMapper.GetMetadata(typeof(T));
        metadata.PrimaryKey.Property.SetValue(entity, Convert.ChangeType(generatedId, metadata.PrimaryKey.Property.PropertyType));

        return 1;
    }

    public int Update<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, values, pkValue) = builder.BuildUpdate(entity);
        var command = _connection.CreateCommand(sql);

        for (var i = 0; i < values.Count; i++)
            _connection.AddParameter(command, $"@p{i}", TypeMapper.ConvertToDb(values[i]));

        _connection.AddParameter(command, "@pk", pkValue);

        return _connection.ExecuteNonQuery(command);
    }

    public Task<int> UpdateAsync<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, values, pkValue) = builder.BuildUpdate(entity);
        var command = _connection.CreateCommand(sql);

        for (var i = 0; i < values.Count; i++)
            _connection.AddParameter(command, $"@p{i}", TypeMapper.ConvertToDb(values[i]));

        _connection.AddParameter(command, "@pk", pkValue);

        return _connection.ExecuteNonQueryAsync(command);
    }

    public int Delete<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, pkValue) = builder.BuildDelete(entity);

        var command = _connection.CreateCommand(sql);
        _connection.AddParameter(command, "@pk", pkValue);

        return _connection.ExecuteNonQuery(command);
    }

    public Task<int> DeleteAsync<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, pkValue) = builder.BuildDelete(entity);

        var command = _connection.CreateCommand(sql);
        _connection.AddParameter(command, "@pk", pkValue);

        return _connection.ExecuteNonQueryAsync(command);
    }

    public List<T> Query<T>(QueryBuilder<T> qb) where T : class
    {
        var command = _connection.CreateCommandWithParameters(qb.Build(), qb.GetParameters());

        var result = new List<T>();
        try
        {
            using var reader = _connection.ExecuteReader(command);
            var metadata = EntityMapper.GetMetadata(typeof(T));

            while (reader.Read())
            {
                T entity;
                if (_lazyLoader != null && _entityTracker != null)
                {
                    // Use lazy loading-aware mapping
                    entity = (T)MapEntityWithLazyLoading(reader, metadata);
                }
                else
                {
                    // Standard mapping
                    entity = EntityMapper.MapEntity<T>(reader, metadata);
                }
                result.Add(entity);
            }
        }
        catch (DbException ex)
        {
            throw new DataAccessException(
                $"Query failed for entity '{typeof(T).Name}'. Check that the database schema is in sync with the model.",
                ex);
        }

        return result;
    }

    public async Task<List<T>> QueryAsync<T>(QueryBuilder<T> qb) where T : class
    {
        var command = _connection.CreateCommandWithParameters(qb.Build(), qb.GetParameters());

        var result = new List<T>();
        try
        {
            await using var reader = await _connection.ExecuteReaderAsync(command);
            var metadata = EntityMapper.GetMetadata(typeof(T));

            while (await reader.ReadAsync())
            {
                T entity;
                if (_lazyLoader != null && _entityTracker != null)
                {
                    // Use lazy loading-aware mapping
                    entity = (T)MapEntityWithLazyLoading(reader, metadata);
                }
                else
                {
                    // Standard mapping
                    entity = EntityMapper.MapEntity<T>(reader, metadata);
                }
                result.Add(entity);
            }
        }
        catch (DbException ex)
        {
            throw new DataAccessException(
                $"Query failed for entity '{typeof(T).Name}'. Check that the database schema is in sync with the model.",
                ex);
        }

        return result;
    }

    public async Task ExecuteSqlAsync(List<string> sqlBatch, CancellationToken ct = default)
    {
        if (sqlBatch.Count == 0)
            return;

        await _connection.BeginTransactionAsync(ct);
        try
        {
            foreach (var sql in sqlBatch)
                await _connection.ExecuteNonQueryAsync(_connection.CreateCommand(sql));
            await _connection.CommitTransactionAsync(ct);
        }
        catch
        {
            await _connection.RollbackTransactionAsync(ct);
            throw;
        }
    }

    private object MapEntityWithLazyLoading(DbDataReader reader, EntityMetadata metadata)
    {
        // Check if entity is already tracked
        var pkOrdinal = reader.GetOrdinal(metadata.PrimaryKey.ColumnName);
        var pkValue = reader.GetValue(pkOrdinal);

        // Try to get tracked entity
        var trackedMethod = typeof(EntityTracker).GetMethod("TryGetTracked")!.MakeGenericMethod(metadata.EntityType);
        var parameters = new[] { pkValue, null };
        var isTracked = (bool)trackedMethod.Invoke(_entityTracker, parameters)!;

        if (isTracked && parameters[1] != null)
        {
            return parameters[1];
        }

        // Create proxy if navigation properties exist
        var hasNavigationProperties = metadata.Relationships.Count > 0;
        
        object entity;
        if (hasNavigationProperties && _navigationLoadState != null)
        {
            entity = LazyLoadingProxyFactory.CreateProxy(metadata.EntityType, _lazyLoader!, _navigationLoadState);
        }
        else
        {
            entity = Activator.CreateInstance(metadata.EntityType)!;
        }

        // Map scalar properties
        foreach (var column in metadata.Columns)
        {
            var ordinal = reader.GetOrdinal(column.ColumnName);
            var value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            var convertedValue = TypeMapper.ConvertFromDb(value, column.Property.PropertyType);
            column.Property.SetValue(entity, convertedValue);
        }

        // Track the entity
        _entityTracker!.TrackEntity(metadata.EntityType, entity, pkValue);

        return entity;
    }
}