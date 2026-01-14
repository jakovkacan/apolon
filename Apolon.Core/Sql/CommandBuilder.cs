using Apolon.Core.Mapping;
using Apolon.Core.Mapping.Models;

namespace Apolon.Core.Sql;

public class CommandBuilder<T> where T : class
{
    private readonly EntityMetadata _metadata = EntityMapper.GetMetadata(typeof(T));

    public (string sql, List<object?> values) BuildInsert(T entity)
    {
        var columns = _metadata.Columns.Where(c => c.PropertyName != _metadata.PrimaryKey.PropertyName).ToList();
        var columnNames = string.Join(", ", columns.Select(c => c.ColumnName));
        var paramNames = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
        var values = columns.Select(c => c.Property.GetValue(entity)).ToList();

        var sql = $"INSERT INTO {_metadata.Schema}.{_metadata.TableName} ({columnNames}) VALUES ({paramNames})";
        return (sql, values);
    }

    public (string sql, List<object?> values, object pkValue) BuildUpdate(T entity)
    {
        var pk = _metadata.PrimaryKey;
        var pkValue = pk.Property.GetValue(entity)!;
        var columns = _metadata.Columns.Where(c => c.PropertyName != pk.PropertyName).ToList();

        var setClause = string.Join(", ", columns.Select((c, i) => $"{c.ColumnName} = @p{i}"));
        var values = columns.Select(c => c.Property.GetValue(entity)).ToList();
        
        var sql = $"UPDATE {_metadata.Schema}.{_metadata.TableName} SET {setClause} WHERE {pk.ColumnName} = @pk";
        return (sql, values, pkValue);
    }

    public (string Sql, object PrimaryKey) BuildDelete(T entity)
    {
        var pk = _metadata.PrimaryKey;
        var pkValue = pk.Property.GetValue(entity)!;
        var sql = $"DELETE FROM {_metadata.Schema}.{_metadata.TableName} WHERE {pk.ColumnName} = @pk";
        return (sql, pkValue);
    }
}