using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Apolon.Core.Attributes;
using Apolon.Core.Mapping;
using Apolon.Core.Query;
using Apolon.Core.DataAccess;
using Npgsql;

namespace Apolon.Core.DbSet;

public class DbSet<T>(DbConnection connection) : IEnumerable<T>
    where T : class
{
    private readonly EntityMetadata _metadata = EntityMapper.GetMetadata(typeof(T));
    private readonly List<T> _localCache = [];
    private readonly ChangeTracker _changeTracker = new();

    // CREATE
    public void Add(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

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

        return ExecuteSql(query.Build(), query.GetParameters()).FirstOrDefault();
    }

    public IQueryable<T> AsQueryable()
    {
        return _localCache.AsQueryable();
    }

    public QueryBuilder<T> Query()
    {
        return new QueryBuilder<T>();
    }

    public List<T> ExecuteQuery(QueryBuilder<T> query)
    {
        return ExecuteSql(query.Build(), query.GetParameters());
    }

    public List<T> ToList()
    {
        var columns = string.Join(", ", _metadata.Columns.Select(c => c.ColumnName));
        var sql = $"SELECT {columns} FROM {_metadata.Schema}.{_metadata.TableName}";

        return ExecuteSql(sql, []);
    }

    public async Task<List<T>> ToListAsync()
    {
        return await Task.FromResult(ToList());
    }

    public List<T> Include(params Expression<Func<T, object>>[] includeProperties)
    {
        var entities = ToList();

        foreach (var includeExpression in includeProperties)
        {
            var memberExpression = GetMemberExpression(includeExpression);
            var navigationProperty = memberExpression.Member as PropertyInfo;

            if (navigationProperty == null)
                continue;

            var navigationPropertyType = navigationProperty.PropertyType;

            // Handle ICollection<TRelated>
            if (navigationPropertyType.IsGenericType &&
                navigationPropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
            {
                var relatedType = navigationPropertyType.GetGenericArguments()[0];
                LoadCollection<T>(entities, navigationProperty, relatedType);
            }
            // Handle single reference (e.g., Checkup.Patient)
            else
            {
                LoadReference(entities, navigationProperty, navigationPropertyType);
            }
        }

        return entities;
    }

    private MemberExpression GetMemberExpression(Expression<Func<T, object>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
            return memberExpression;

        if (expression.Body is UnaryExpression unaryExpression)
            return unaryExpression.Operand as MemberExpression;

        throw new ArgumentException("Invalid include expression");
    }

    private void LoadCollection<TRelated>(List<T> entities, PropertyInfo navigationProperty, Type relatedType)
    {
        var relatedMetadata = EntityMapper.GetMetadata(relatedType);

        // Find foreign key column that points to current entity
        var foreignKeyColumn = relatedMetadata.Columns
            .FirstOrDefault(c => c.Property.GetCustomAttribute<ForeignKeyAttribute>()?.ReferencedTable == typeof(T));

        if (foreignKeyColumn == null)
            return;

        var primaryKey = _metadata.PrimaryKey;
        var entityIds = entities.Select(e => primaryKey.Property.GetValue(e)).ToList();

        if (entityIds.Count == 0)
            return;

        // Build query: SELECT * FROM related_table WHERE foreign_key_column IN (@ids)
        var columns = string.Join(", ", relatedMetadata.Columns.Select(c => c.ColumnName));
        var sql = $"SELECT {columns} FROM {relatedMetadata.Schema}.{relatedMetadata.TableName} " +
                  $"WHERE {foreignKeyColumn.ColumnName} = ANY(@ids)";

        var command = connection.CreateCommand(sql);
        var idsArray = entityIds.ToArray();
        var parameter = command.Parameters.Add("@ids", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer);
        parameter.Value = idsArray;


        var relatedEntities = new Dictionary<object, List<object>>();

        using (var reader = connection.ExecuteReader(command))
        {
            while (reader.Read())
            {
                var relatedEntity = MapRelatedEntity(reader, relatedMetadata);
                var foreignKeyValue = foreignKeyColumn.Property.GetValue(relatedEntity);

                if (!relatedEntities.ContainsKey(foreignKeyValue))
                    relatedEntities[foreignKeyValue] = new List<object>();

                relatedEntities[foreignKeyValue].Add(relatedEntity);
            }
        }

        // Populate navigation properties
        foreach (var entity in entities)
        {
            var entityId = primaryKey.Property.GetValue(entity);

            if (relatedEntities.TryGetValue(entityId, out var related))
            {
                var collectionType = typeof(List<>).MakeGenericType(relatedType);
                var collection = Activator.CreateInstance(collectionType);

                foreach (var item in related)
                {
                    collectionType.GetMethod("Add").Invoke(collection, new[] { item });
                }

                navigationProperty.SetValue(entity, collection);
            }
        }
    }

    private void LoadReference(List<T> entities, PropertyInfo navigationProperty, Type relatedType)
    {
        var relatedMetadata = EntityMapper.GetMetadata(relatedType);

        // Find foreign key in current entity that references related entity
        var foreignKeyColumn = _metadata.Columns
            .FirstOrDefault(c => c.Property.GetCustomAttribute<ForeignKeyAttribute>()?.ReferencedTable == relatedType);

        if (foreignKeyColumn == null)
            return;

        var relatedIds = entities
            .Select(e => foreignKeyColumn.Property.GetValue(e))
            .Where(id => id != null)
            .Distinct()
            .ToList();

        if (!relatedIds.Any())
            return;

        var columns = string.Join(", ", relatedMetadata.Columns.Select(c => c.ColumnName));
        var sql = $"SELECT {columns} FROM {relatedMetadata.Schema}.{relatedMetadata.TableName} " +
                  $"WHERE {relatedMetadata.PrimaryKey.ColumnName} = ANY(@ids)";

        var command = connection.CreateCommand(sql);
        var idsArray = relatedIds.ToArray();
        var parameter = command.Parameters.Add("@ids", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer);
        parameter.Value = idsArray;


        var relatedEntities = new Dictionary<object, object>();

        using (var reader = connection.ExecuteReader(command))
        {
            while (reader.Read())
            {
                var relatedEntity = MapRelatedEntity(reader, relatedMetadata);
                var id = relatedMetadata.PrimaryKey.Property.GetValue(relatedEntity);
                relatedEntities[id] = relatedEntity;
            }
        }

        foreach (var entity in entities)
        {
            var foreignKeyValue = foreignKeyColumn.Property.GetValue(entity);

            if (foreignKeyValue != null && relatedEntities.TryGetValue(foreignKeyValue, out var related))
            {
                navigationProperty.SetValue(entity, related);
            }
        }
    }

    private object MapRelatedEntity(NpgsqlDataReader reader, EntityMetadata metadata)
    {
        var entity = Activator.CreateInstance(metadata.EntityType);

        foreach (var column in metadata.Columns)
        {
            var ordinal = reader.GetOrdinal(column.ColumnName);
            var value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            var convertedValue = TypeMapper.ConvertFromDb(value, column.Property.PropertyType);
            column.Property.SetValue(entity, convertedValue);
        }

        return entity;
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
        var affectedRows = 0;

        // Insert new entities
        foreach (var entity in _changeTracker.NewEntities)
        {
            affectedRows += InsertEntity((T)entity);
        }

        // Update modified entities
        foreach (var entity in _changeTracker.ModifiedEntities)
        {
            affectedRows += UpdateEntity((T)entity);
        }

        // Delete removed entities
        foreach (var entity in _changeTracker.DeletedEntities)
        {
            affectedRows += DeleteEntity((T)entity);
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

        var command = connection.CreateCommand(sql);
        var i = 0;
        foreach (var column in columns)
        {
            var value = column.Property.GetValue(entity);
            command.Parameters.AddWithValue($"@p{i++}", TypeMapper.ConvertToDb(value));
        }

        return connection.ExecuteNonQuery(command);
    }

    private int UpdateEntity(T entity)
    {
        var pk = _metadata.PrimaryKey;
        var pkValue = pk.Property.GetValue(entity);
        var columns = _metadata.Columns.Where(c => c.PropertyName != pk.PropertyName);

        var setClause = string.Join(", ", columns.Select((c, i) => $"{c.ColumnName} = @p{i}"));
        var sql = $"UPDATE {_metadata.Schema}.{_metadata.TableName} SET {setClause} WHERE {pk.ColumnName} = @pk";

        var command = connection.CreateCommand(sql);
        var i = 0;
        foreach (var column in columns)
        {
            var value = column.Property.GetValue(entity);
            command.Parameters.AddWithValue($"@p{i++}", TypeMapper.ConvertToDb(value));
        }

        command.Parameters.AddWithValue("@pk", pkValue);

        return connection.ExecuteNonQuery(command);
    }

    private int DeleteEntity(T entity)
    {
        var pk = _metadata.PrimaryKey;
        var pkValue = pk.Property.GetValue(entity);
        var sql = $"DELETE FROM {_metadata.Schema}.{_metadata.TableName} WHERE {pk.ColumnName} = @pk";

        var command = connection.CreateCommand(sql);
        command.Parameters.AddWithValue("@pk", pkValue);

        return connection.ExecuteNonQuery(command);
    }

    // Helper: Execute query
    private List<T> ExecuteSql(string sql, List<ParameterMapping> parameters)
    {
        var command = connection.CreateCommand(sql);
        foreach (var param in parameters)
        {
            command.Parameters.AddWithValue(param.Name, param.Value ?? DBNull.Value);
        }

        var result = new List<T>();
        using var reader = connection.ExecuteReader(command);

        while (reader.Read())
        {
            result.Add(MapEntity(reader));
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