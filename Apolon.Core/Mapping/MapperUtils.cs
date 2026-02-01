using System.Reflection;
using System.Text;
using Apolon.Core.Attributes;

namespace Apolon.Core.Mapping;

internal static class MapperUtils
{
    public static bool IsPrimitiveOrSimpleType(Type type)
    {
        return type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) ||
               type == typeof(decimal) || type == typeof(Guid) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
    }

    public static bool IsPersistentType(Type type)
    {
        return type.GetCustomAttribute<TableAttribute>() != null;
    }

    public static string ConvertPascalToSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        var result = new StringBuilder();
        result.Append(char.ToLower(pascalCase[0]));

        for (var i = 1; i < pascalCase.Length; i++)
            if (char.IsUpper(pascalCase[i]))
            {
                result.Append('_');
                result.Append(char.ToLower(pascalCase[i]));
            }
            else
            {
                result.Append(pascalCase[i]);
            }

        return result.ToString();
    }
}