using System.Linq.Expressions;
using Apolon.Core.Exceptions;
using Apolon.Core.Mapping;

namespace Apolon.Core.SqlBuilders;

public class QueryBuilder<T> where T : class
{
    private readonly EntityMetadata _metadata = EntityMapper.GetMetadata(typeof(T));
    private readonly List<string> _whereClauses = [];
    private readonly List<ParameterMapping> _parameters = [];
    private string _orderByClause = "";
    private int _parameterCounter = 0;

    public QueryBuilder<T> Where(Expression<Func<T, bool>> predicate)
    {
        var sql = TranslateExpression(predicate.Body);
        _whereClauses.Add(sql);
        return this;
    }
    
    public QueryBuilder<T> WhereRaw(string clause, object parameterValue)
    {
        var paramName = AddParameter(parameterValue);
        // Replace the placeholder in your raw clause with the generated parameter name
        _whereClauses.Add(clause.Replace("{0}", paramName));
        return this;
    }

    public QueryBuilder<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        var memberExpr = keySelector.Body as MemberExpression;
        var columnName = GetColumnName(memberExpr?.Member.Name);
        _orderByClause = $"ORDER BY {columnName} ASC";
        return this;
    }

    public QueryBuilder<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        var memberExpr = keySelector.Body as MemberExpression;
        var columnName = GetColumnName(memberExpr?.Member.Name);
        _orderByClause += $", {columnName} DESC";
        return this;
    }

    public string Build()
    {
        var sql = BuildSelectClause();

        if (_whereClauses.Count > 0)
        {
            sql += " WHERE " + string.Join(" AND ", _whereClauses);
        }

        if (!string.IsNullOrEmpty(_orderByClause))
        {
            sql += " " + _orderByClause;
        }

        return sql;
    }

    private string BuildSelectClause()
    {
        var columns = string.Join(", ",
            _metadata.Columns.Select(c => $"{c.ColumnName}"));
        return $"SELECT {columns} FROM {_metadata.Schema}.{_metadata.TableName}";
    }

    private string TranslateExpression(Expression expr)
    {
        return expr switch
        {
            BinaryExpression binExpr => TranslateBinaryExpression(binExpr),
            MethodCallExpression methodExpr => TranslateMethodCall(methodExpr),
            MemberExpression memberExpr => GetColumnName(memberExpr.Member.Name),
            ConstantExpression constExpr => AddParameter(constExpr.Value),
            _ => throw new OrmException($"Unsupported expression: {expr.GetType()}")
        };
    }

    private string TranslateBinaryExpression(BinaryExpression expr)
    {
        var left = TranslateExpression(expr.Left);
        var right = TranslateExpression(expr.Right);
        var op = expr.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "!=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.AndAlso => "AND",
            ExpressionType.OrElse => "OR",
            _ => throw new OrmException($"Unsupported operator: {expr.NodeType}")
        };

        return $"({left} {op} {right})";
    }

    private string TranslateMethodCall(MethodCallExpression expr)
    {
        if (expr.Method.Name == "Contains" && expr.Object is MemberExpression memberExpr)
        {
            var columnName = GetColumnName(memberExpr.Member.Name);
            var argument = ((ConstantExpression)expr.Arguments[0]).Value;
            var paramName = AddParameter(argument);
            return $"{columnName} LIKE {paramName}";
        }

        throw new OrmException($"Unsupported method: {expr.Method.Name}");
    }

    private string GetColumnName(string propertyName)
    {
        var column = _metadata.Columns.FirstOrDefault(c => c.PropertyName == propertyName);
        return column?.ColumnName ?? throw new OrmException($"Property {propertyName} not found");
    }

    private string AddParameter(object value)
    {
        var paramName = $"@param{_parameterCounter++}";
        _parameters.Add(new ParameterMapping { Name = paramName, Value = value });
        return paramName;
    }

    public List<ParameterMapping> GetParameters() => _parameters;
}

public class ParameterMapping
{
    public string Name { get; set; }
    public object Value { get; set; }
}