namespace Apolon.Core.Migrations;

internal static class SnapshotNormalization
{
    public static string NormalizeIdentifier(string value)
        => value.Trim().Trim('"').ToLowerInvariant();

    public static string NormalizeDataType(string dataType)
    {
        var t = CollapseWhitespace(dataType).ToLowerInvariant();

        if (t.Contains('('))
            t = t[..t.IndexOf('(')];

        return t switch
        {
            "character varying" => "varchar",
            "character" => "char",
            "timestamp without time zone" => "timestamp",
            "timestamp with time zone" => "timestamptz",
            "time without time zone" => "time",
            "time with time zone" => "timetz",
            "double precision" => "float8",
            "real" => "float4",
            "integer" or "int" => "int4",
            "decimal" => "numeric",
            _ => t
        };
    }

    public static (int? CharacterMaximumLength, int? NumericPrecision, int? NumericScale)
        ExtractDataTypeDetails(string dataType)
    {
        var t = CollapseWhitespace(dataType).ToLowerInvariant();
        var parts = t.Split('(', StringSplitOptions.RemoveEmptyEntries);

        var baseType = parts[0];
        var parameters = parts.Length > 1 ? parts[1][..^1].Split(',') : [];

        if (parameters.Length == 0)
        {
            return baseType switch
            {
                "int" or "int4" => (null, 32, 0),
                "decimal" => (null, 18, 2),
                "double precision" => (null, 53, null),
                "real" => (null, 24, null),
                "varchar" => (255, null, null),
                _ => (null, null, null)
            };
        }

        return baseType switch
        {
            "int4" => (null, int.Parse(parameters[0]),
                parameters.Length <= 1 ? 0 : int.Parse(parameters[1])),
            "numeric" => (null, int.Parse(parameters[1]),
                parameters.Length <= 1 ? 0 : int.Parse(parameters[2])),
            "varchar" => (int.Parse(parameters[0]), null, null),
            "decimal" => (null, int.Parse(parameters[0]),
                parameters.Length <= 1 ? 2 : int.Parse(parameters[1])),
            _ => (null, null, null)
        };
    }

    public static string? NormalizeDefault(string? columnDefault)
    {
        if (string.IsNullOrWhiteSpace(columnDefault))
            return null;

        var d = CollapseWhitespace(columnDefault).Trim();

        while (d.Length >= 2 && d[0] == '(' && d[^1] == ')' && ParenthesesWrapWholeExpression(d))
            d = d[1..^1].Trim();

        d = StripPostgresCasts(d);
        d = d.Replace("now()", "current_timestamp", StringComparison.OrdinalIgnoreCase);

        return d;
    }

    internal static string CollapseWhitespace(string s)
        => string.Join(' ', s.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

    private static bool ParenthesesWrapWholeExpression(string s)
    {
        var depth = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')') depth--;

            if (depth == 0 && i < s.Length - 1)
                return false;
        }

        return s.Length >= 2 && s[0] == '(' && s[^1] == ')' && depth == 0;
    }

    private static string StripPostgresCasts(string s)
    {
        var result = new System.Text.StringBuilder(s.Length);
        var inSingleQuote = false;

        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];

            if (ch == '\'')
            {
                inSingleQuote = !inSingleQuote;
                result.Append(ch);
                continue;
            }

            if (!inSingleQuote && ch == ':' && i + 1 < s.Length && s[i + 1] == ':')
            {
                i += 2;
                while (i < s.Length)
                {
                    var c = s[i];
                    if (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '[' || c == ']')
                    {
                        i++;
                        continue;
                    }

                    break;
                }

                i -= 1;
                continue;
            }

            result.Append(ch);
        }

        return result.ToString().Trim();
    }
}