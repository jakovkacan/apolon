using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Apolon.Core.Mapping;
using Apolon.Core.Query;
using Apolon.Core.DataAccess;

namespace Apolon.Core.DbSet;

public class DbSet<T> : IEnumerable<T> where T : class
{
    private readonly DbConnection _connection;
    private readonly EntityMetadata _metadata;
    private readonly List<T> _localCache = new();
    private readonly ChangeTracker _changeTracker;

    public DbSet(DbConnection connection)
    {
        _connection = connection;
        _metadata = EntityMapper.GetMetadata(typeof(T));
        _changeTracker = new ChangeTracker();
    }

    // CREATE
    public void Add(T entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        _localCache.Add(entity);
        _changeTracker.TrackNew(entity);
    }

    public async Task AddAsync(T entity)
    {
        Add(entity);
        await Task.CompletedTask;
    }

    // READ
    public T Find(int id)
    {
        var query = new QueryBuilder<T>()
            .Where(e => (int)e.GetType().GetProperty(_metadata.PrimaryKey.PropertyName)
                .GetValue(e) == id);
        
        return ToList(query.Build(), query.GetParameters()).FirstOrDefault();
    }

    public IQueryable<T> AsQueryable()
    {
        return _localCache.AsQueryable();
    }

    public QueryBuilder<T> Query()
    {
        return new QueryBuilder<T>();
    }

    // UPDATE
    public void Update(T entity)
    {
        var index = _localCache.IndexOf(entity);
        if (index >= 0)
        {
            _localCache[index] = entity;
        }
        _changeTracker.TrackModified(entity);
    }

    // DELETE
    public bool Remove(T entity)
    {
        _changeTracker.TrackDeleted(entity);
        return _localCache.Remove(entity);
    }

    // SAVE
    public int SaveChanges()
    {
        int affectedRows = 0;
        
        // Insert new entities
        foreach (var entity in _changeTracker.NewEntities)
        {
            affectedRows += InsertEntity(entity);
        }

        // Update modified entities
        foreach (var entity in _changeTracker.ModifiedEntities)
        {
            affectedRows += UpdateEntity(entity);
        }

        // Delete removed entities
        foreach (var entity in _changeTracker.DeletedEntities)
        {
            affectedRows += DeleteEntity(entity);
        }

        _changeTracker.Clear();
        return affectedRows;
    }

    private int InsertEntity(T entity)
    {
        var columns = _metadata.Columns.Where(c => c.PropertyName != _metadata.PrimaryKey.PropertyName);
        var columnNames = string.Join(", ", columns.Select(c => c.ColumnName));
        var paramNames = string.Join(", ", columns.Select((_, i) => $"@p{i}"));

        var sql = $"INSERT INTO {_metadata.Schema}.{_metadata.TableName} ({columnNames}) VALUES ({paramNames})";

        var command = _connection.CreateCommand(sql);
        var i = 0;
        foreach (var column in columns)
        {
            var value = column.Property.GetValue(entity);
            command.Parameters.AddWithValue($"@p{i++}", TypeMapper.ConvertToDb(value));
        }

        return _connection.ExecuteNonQuery(command);
    }

    private int UpdateEntity(T entity)
    {
        var pk = _metadata.PrimaryKey;
        var pkValue = pk.Property.GetValue(entity);
        var columns = _metadata.Columns.Where(c => c.PropertyName != pk.PropertyName);

        var setClause = string.Join(", ", columns.Select((c, i) => $"{c.ColumnName} = @p{i}"));
        var sql = $"UPDATE {_metadata.Schema}.{_metadata.TableName} SET {setClause} WHERE {pk.ColumnName} = @pk";

        var command = _connection.CreateCommand(sql);
        var i = 0;
        foreach (var column in columns)
        {
            var value = column.Property.GetValue(entity);
            command.Parameters.AddWithValue($"@p{i++}", TypeMapper.ConvertToDb(value));
        }
        command.Parameters.AddWithValue("@pk", pkValue);

        return _connection.ExecuteNonQuery(command);
    }

    private int DeleteEntity(T entity)
    {
        var pk = _metadata.PrimaryKey;
        var pkValue = pk.Property.GetValue(entity);
        var sql = $"DELETE FROM {_metadata.Schema}.{_metadata.TableName} WHERE {pk.ColumnName} = @pk";

        var command = _connection.CreateCommand(sql);
        command.Parameters.AddWithValue("@pk", pkValue);

        return _connection.ExecuteNonQuery(command);
    }

    // Helper: Execute query
    private List<T> ToList(string sql, List<ParameterMapping> parameters)
    {
        var command = _connection.CreateCommand(sql);
        foreach (var param in parameters)
        {
            command.Parameters.AddWithValue(param.Name, param.Value ?? DBNull.Value);
        }

        var result = new List<T>();
        using (var reader = _connection.ExecuteReader(command))
        {
            while (reader.Read())
            {
                result.Add(MapEntity(reader));
            }
        }

        return result;
    }

    private T MapEntity(NpgsqlDataReader reader)
    {
        var entity = (T)Activator.CreateInstance(typeof(T));

        foreach (var column in _metadata.Columns)
        {
            var ordinal = reader.GetOrdinal(column.ColumnName);
            var value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            var convertedValue = TypeMapper.ConvertFromDb(value, column.Property.PropertyType);
            column.Property.SetValue(entity, convertedValue);
        }

        return entity;
    }

    public IEnumerator<T> GetEnumerator() => _localCache.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}