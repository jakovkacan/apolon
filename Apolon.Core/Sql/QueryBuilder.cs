using System.Linq.Expressions;
using Apolon.Core.Exceptions;
using Apolon.Core.Mapping;
using Apolon.Core.Mapping.Models;

namespace Apolon.Core.Sql;

public class QueryBuilder<T> where T : class
{
    private readonly EntityMetadata _metadata = EntityMapper.GetMetadata(typeof(T));
    private readonly List<string> _whereClauses = [];
    private readonly List<ParameterMapping> _parameters = [];
    private readonly List<string> _orderClauses = [];
    private int? _limit;
    private int? _offset;
    private int _parameterCounter = 0;

    public List<ParameterMapping> GetParameters() => _parameters;

    public QueryBuilder<T> Where(Expression<Func<T, bool>> predicate)
    {
        var sql = TranslateExpression(predicate.Body);
        _whereClauses.Add(sql);
        return this;
    }

    public QueryBuilder<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector) => AddOrder(keySelector, "ASC");

    public QueryBuilder<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector) =>
        AddOrder(keySelector, "DESC");


    public QueryBuilder<T> WhereRaw(string clause, object parameterValue)
    {
        var paramName = AddParameter(parameterValue);
        // Replace the placeholder in your raw clause with the generated parameter name
        _whereClauses.Add(clause.Replace("{0}", paramName));
        return this;
    }

    private QueryBuilder<T> AddOrder<TKey>(Expression<Func<T, TKey>> keySelector, string direction)
    {
        var memberExpr = GetMemberExpression(keySelector.Body);
        var columnName = GetColumnName(memberExpr.Member.Name);
        _orderClauses.Add($"{columnName} {direction}");
        return this;
    }

    public QueryBuilder<T> Take(int count)
    {
        _limit = count;
        return this;
    }

    public QueryBuilder<T> Skip(int count)
    {
        _offset = count;
        return this;
    }

    public string Build()
    {
        var sql = BuildSelectClause();

        if (_whereClauses.Count > 0)
        {
            sql += " WHERE " + string.Join(" AND ", _whereClauses);
        }

        if (_orderClauses.Count > 0)
            sql += " ORDER BY " + string.Join(", ", _orderClauses);

        if (_limit.HasValue) sql += $" LIMIT {_limit}";
        if (_offset.HasValue) sql += $" OFFSET {_offset}";

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
            ConstantExpression constExpr => AddParameter(constExpr.Value ?? DBNull.Value),
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

    private MemberExpression GetMemberExpression(Expression expr)
    {
        return expr switch
        {
            MemberExpression me => me,
            UnaryExpression { Operand: MemberExpression ume } => ume,
            _ => throw new OrmException("Invalid expression: expected a property.")
        };
    }
}

public class ParameterMapping
{
    public required string Name { get; init; }
    public required object Value { get; init; }
}