using System.Data.Common;
using Apolon.Core.Mapping;
using Apolon.Core.SqlBuilders;

namespace Apolon.Core.DataAccess;

internal class EntityExecutor(IDbConnection connection)
{
    public int Insert<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, values) = builder.BuildInsert(entity);
        var command = connection.CreateCommand(sql);

        for (var i = 0; i < values.Count; i++)
            connection.AddParameter(command, $"@p{i}", TypeMapper.ConvertToDb(values[i]));

        return connection.ExecuteNonQuery(command);
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

    public int Delete<T>(T entity) where T : class
    {
        var builder = new CommandBuilder<T>();
        var (sql, pkValue) = builder.BuildDelete(entity);

        var command = connection.CreateCommand(sql);
        connection.AddParameter(command, "@pk", pkValue);

        return connection.ExecuteNonQuery(command);
    }

    public List<T> Query<T>(QueryBuilder<T> qb) where T : class
    {
        var command = connection.CreateCommand(qb.Build());
        foreach (var param in qb.GetParameters())
        {
            connection.AddParameter(command, param.Name, param.Value);
        }

        var result = new List<T>();
        using var reader = connection.ExecuteReader(command);
        var metadata = EntityMapper.GetMetadata(typeof(T));

        while (reader.Read())
        {
            result.Add(MapEntity<T>(reader, metadata));
        }

        return result;
    }

    public static T MapEntity<T>(DbDataReader reader, EntityMetadata metadata) where T : class
    {
        var entity = Activator.CreateInstance<T>();
        foreach (var column in metadata.Columns)
        {
            var ordinal = reader.GetOrdinal(column.ColumnName);
            var value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            column.Property.SetValue(entity, TypeMapper.ConvertFromDb(value, column.Property.PropertyType));
        }

        return entity;
    }

    public static object MapEntity(DbDataReader reader, EntityMetadata metadata)
    {
        var entity = Activator.CreateInstance(metadata.EntityType)
                     ?? throw new InvalidOperationException($"Could not create instance of {metadata.EntityType.Name}");

        foreach (var column in metadata.Columns)
        {
            var ordinal = reader.GetOrdinal(column.ColumnName);
            var value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            var convertedValue = TypeMapper.ConvertFromDb(value, column.Property.PropertyType);
            column.Property.SetValue(entity, convertedValue);
        }

        return entity;
    }
}