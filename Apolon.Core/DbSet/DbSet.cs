using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Apolon.Core.Attributes;
using Apolon.Core.DataAccess;
using Apolon.Core.Mapping;
using Apolon.Core.Mapping.Models;
using Apolon.Core.Sql;
using Npgsql;

namespace Apolon.Core.DbSet;

public class DbSet<T> : IEnumerable<T>
    where T : class
{
    private readonly IDbConnection _connection;
    private readonly EntityMetadata _metadata = EntityMapper.GetMetadata(typeof(T));
    private readonly List<T> _localCache = [];
    private readonly EntityExecutor _executor;
    private readonly ChangeTracker _changeTracker = new();

    internal DbSet(IDbConnection connection)
    {
        _connection = connection;
        _executor = new EntityExecutor(connection);
    }

    // CREATE
    public void Add(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        _localCache.Add(entity);
        _changeTracker.TrackNew(entity);
    }

    // READ
    public T? Find(int id)
    {
        var primaryKeyProperty = _metadata.PrimaryKey.Property;

        var query = Query().Where(e =>
            Convert.ToInt32(primaryKeyProperty.GetValue(e)) == id);

        return _executor.Query(query).FirstOrDefault();
    }

    public async Task<T?> FindAsync(int id)
    {
        var primaryKeyProperty = _metadata.PrimaryKey.Property;

        var query = Query().Where(e =>
            Convert.ToInt32(primaryKeyProperty.GetValue(e)) == id);

        var list = await _executor.QueryAsync(query);
        return list.FirstOrDefault();
    }

    public IQueryable<T> AsQueryable()
    {
        return _localCache.AsQueryable();
    }

    public QueryBuilder<T> Query()
    {
        return new QueryBuilder<T>();
    }

    public List<T> ExecuteQuery(QueryBuilder<T> queryBuilder)
    {
        return _executor.Query(queryBuilder);
    }

    public Task<List<T>> ExecuteQueryAsync(QueryBuilder<T> queryBuilder)
    {
        return _executor.QueryAsync(queryBuilder);
    }

    public List<T> ToList() => _executor.Query(new QueryBuilder<T>());

    public Task<List<T>> ToListAsync()
    {
        return _executor.QueryAsync(new QueryBuilder<T>());
    }

    public List<T> Include(params Expression<Func<T, object>>[] includeProperties)
    {
        var entities = ToList();

        foreach (var includeExpression in includeProperties)
        {
            var memberExpression = GetMemberExpression(includeExpression);
            var navigationProperty = memberExpression?.Member as PropertyInfo;

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
        var affectedRows = _changeTracker.NewEntities.Sum(entity => _executor.Insert((T)entity))
                           + _changeTracker.ModifiedEntities.Sum(entity => _executor.Update((T)entity))
                           + _changeTracker.DeletedEntities.Sum(entity => _executor.Delete((T)entity));

        _changeTracker.Clear();
        return affectedRows;
    }

    public async Task<int> SaveChangesAsync()
    {
        var affectedRows = 0;

        foreach (var entity in _changeTracker.NewEntities)
            affectedRows += await _executor.InsertAsync((T)entity);

        foreach (var entity in _changeTracker.ModifiedEntities)
            affectedRows += await _executor.UpdateAsync((T)entity);

        foreach (var entity in _changeTracker.DeletedEntities)
            affectedRows += await _executor.DeleteAsync((T)entity);

        _changeTracker.Clear();
        return affectedRows;
    }

    public IEnumerator<T> GetEnumerator() => _localCache.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static MemberExpression? GetMemberExpression(Expression<Func<T, object>> expression)
    {
        return expression.Body switch
        {
            MemberExpression memberExpression => memberExpression,
            UnaryExpression unaryExpression => unaryExpression.Operand as MemberExpression,
            _ => throw new ArgumentException("Invalid include expression")
        };
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

        // SELECT * FROM related_table WHERE foreign_key_column IN (@ids)
        var qb = GetQueryBuilderForType(relatedType);

        qb.WhereRaw($"{foreignKeyColumn.ColumnName} = ANY({{0}})", entityIds.ToArray());

        var command = _connection.CreateCommand(qb.Build());
        foreach (var param in qb.GetParameters())
        {
            _connection.AddParameter(command, param.Name, param.Value);
        }

        var relatedEntities = new Dictionary<object, List<object>>();

        using (var reader = _connection.ExecuteReader(command))
        {
            while (reader.Read())
            {
                var relatedEntity = EntityExecutor.MapEntity(reader, relatedMetadata);
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

            if (entityId == null || !relatedEntities.TryGetValue(entityId, out var related)) continue;

            var collectionType = typeof(List<>).MakeGenericType(relatedType);
            var collection = Activator.CreateInstance(collectionType);

            foreach (var item in related)
            {
                collectionType.GetMethod("Add")?.Invoke(collection, [item]);
            }

            navigationProperty.SetValue(entity, collection);
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

        if (relatedIds.Count == 0)
            return;

        var qb = GetQueryBuilderForType(relatedType);

        qb.WhereRaw($"{relatedMetadata.PrimaryKey.ColumnName} = ANY({{0}})", relatedIds.ToArray());

        DbCommand command = _connection.CreateCommand(qb.Build());
        foreach (ParameterMapping param in qb.GetParameters())
        {
            _connection.AddParameter(command, param.Name, param.Value);
        }

        var relatedEntities = new Dictionary<object, object>();

        using (var reader = _connection.ExecuteReader(command))
        {
            while (reader.Read())
            {
                var relatedEntity = EntityExecutor.MapEntity(reader, relatedMetadata);
                var id = relatedMetadata.PrimaryKey.Property.GetValue(relatedEntity) ??
                         throw new Exception("Primary key is null");
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

    private static dynamic GetQueryBuilderForType(Type entityType)
    {
        var queryBuilderType = typeof(QueryBuilder<>).MakeGenericType(entityType);
        return Activator.CreateInstance(queryBuilderType)!;
    }
}