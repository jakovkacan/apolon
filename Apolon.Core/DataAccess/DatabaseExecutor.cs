using System.Data.Common;
using Apolon.Core.Exceptions;
using Apolon.Core.Mapping;
using Apolon.Core.Sql;

namespace Apolon.Core.DataAccess;

internal class DatabaseExecutor(IDbConnection connection)
{
    public int Insert<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, values) = builder.BuildInsert(entity);
        var command = connection.CreateCommand(sql);

        for (var i = 0; i < values.Count; i++)
            connection.AddParameter(command, $"@p{i}", TypeMapper.ConvertToDb(values[i]));

        var generatedId = connection.ExecuteScalar(command);
    
        // Set the generated ID back on the entity
        var metadata = EntityMapper.GetMetadata(typeof(T));
        metadata.PrimaryKey.Property.SetValue(entity, Convert.ChangeType(generatedId, metadata.PrimaryKey.Property.PropertyType));

        return 1;
    }

    public async Task<int> InsertAsync<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, values) = builder.BuildInsert(entity);
        var command = connection.CreateCommand(sql);

        for (var i = 0; i < values.Count; i++)
            connection.AddParameter(command, $"@p{i}", TypeMapper.ConvertToDb(values[i]));
        
        var generatedId = await connection.ExecuteScalarAsync(command);
    
        // Set the generated ID back on the entity
        var metadata = EntityMapper.GetMetadata(typeof(T));
        metadata.PrimaryKey.Property.SetValue(entity, Convert.ChangeType(generatedId, metadata.PrimaryKey.Property.PropertyType));

        return 1;
    }

    public int Update<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, values, pkValue) = builder.BuildUpdate(entity);
        var command = connection.CreateCommand(sql);

        for (var i = 0; i < values.Count; i++)
            connection.AddParameter(command, $"@p{i}", TypeMapper.ConvertToDb(values[i]));

        connection.AddParameter(command, "@pk", pkValue);

        return connection.ExecuteNonQuery(command);
    }

    public Task<int> UpdateAsync<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, values, pkValue) = builder.BuildUpdate(entity);
        var command = connection.CreateCommand(sql);

        for (var i = 0; i < values.Count; i++)
            connection.AddParameter(command, $"@p{i}", TypeMapper.ConvertToDb(values[i]));

        connection.AddParameter(command, "@pk", pkValue);

        return connection.ExecuteNonQueryAsync(command);
    }

    public int Delete<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, pkValue) = builder.BuildDelete(entity);

        var command = connection.CreateCommand(sql);
        connection.AddParameter(command, "@pk", pkValue);

        return connection.ExecuteNonQuery(command);
    }

    public Task<int> DeleteAsync<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, pkValue) = builder.BuildDelete(entity);

        var command = connection.CreateCommand(sql);
        connection.AddParameter(command, "@pk", pkValue);

        return connection.ExecuteNonQueryAsync(command);
    }

    public List<T> Query<T>(QueryBuilder<T> qb) where T : class
    {
        var command = connection.CreateCommandWithParameters(qb.Build(), qb.GetParameters());

        var result = new List<T>();
        try
        {
            using var reader = connection.ExecuteReader(command);
            var metadata = EntityMapper.GetMetadata(typeof(T));

            while (reader.Read()) result.Add(EntityMapper.MapEntity<T>(reader, metadata));
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
        var command = connection.CreateCommandWithParameters(qb.Build(), qb.GetParameters());

        var result = new List<T>();
        try
        {
            await using var reader = await connection.ExecuteReaderAsync(command);
            var metadata = EntityMapper.GetMetadata(typeof(T));

            while (await reader.ReadAsync()) result.Add(EntityMapper.MapEntity<T>(reader, metadata));
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

        await connection.BeginTransactionAsync(ct);
        try
        {
            foreach (var sql in sqlBatch)
                await connection.ExecuteNonQueryAsync(connection.CreateCommand(sql));
            await connection.CommitTransactionAsync(ct);
        }
        catch
        {
            await connection.RollbackTransactionAsync(ct);
            throw;
        }
    }
}