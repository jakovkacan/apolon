using Apolon.Core.Exceptions;

namespace Apolon.Core.Mapping;

public static class TypeMapper
{
    private static readonly Dictionary<Type, string> CSharpToPostgresTypeMap = new()
    {
        // Numeric types
        { typeof(int), "INT" },
        { typeof(long), "BIGINT" },
        { typeof(short), "SMALLINT" },
        { typeof(decimal), "DECIMAL(18,2)" },
        { typeof(float), "FLOAT" },
        { typeof(double), "DOUBLE PRECISION" },
        
        // String types
        { typeof(string), "VARCHAR(255)" },
        
        // Date/Time types
        { typeof(DateTime), "TIMESTAMP" },
        { typeof(DateTimeOffset), "TIMESTAMP WITH TIME ZONE" },
        
        // Boolean
        { typeof(bool), "BOOLEAN" },
        
        // GUID
        { typeof(Guid), "UUID" },
        
        // Binary
        { typeof(byte[]), "BYTEA" }
    };

    public static string GetPostgresType(Type csharpType)
    {
        // for nullable types
        var underlyingType = Nullable.GetUnderlyingType(csharpType);
        
        csharpType = underlyingType ?? csharpType;

        return CSharpToPostgresTypeMap.TryGetValue(csharpType, out var pgType)
            ? pgType
            : throw new OrmException($"Unsupported type: {csharpType.Name}");
    }

    public static object? ConvertFromDb(object? dbValue, Type targetType)
    {
        if (dbValue is null or DBNull)
            return null;

        // for nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        
        targetType = underlyingType ?? targetType;

        // special conversions
        if (targetType == typeof(DateTime) && dbValue is DateTime dt)
            return dt;

        if (targetType == typeof(bool) && dbValue is bool b)
            return b;

        if (targetType == typeof(Guid) && dbValue is Guid g)
            return g;
        
        return Convert.ChangeType(dbValue, targetType);
    }

    public static object ConvertToDb(object? value)
    {
        return value ?? DBNull.Value;
    }
}