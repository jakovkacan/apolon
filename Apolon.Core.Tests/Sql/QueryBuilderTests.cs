using System.Linq.Expressions;
using Apolon.Core.Attributes;
using Apolon.Core.Exceptions;
using Apolon.Core.Sql;

namespace Apolon.Core.Tests.Sql;

public class QueryBuilderTests
{
    [Fact]
    public void Build_WithNoConditions_GeneratesSimpleSelectQuery()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        var sql = builder.Build();

        Assert.Contains("SELECT id, name, age FROM public.test_entity", sql);
    }

    [Fact]
    public void Where_WithSimpleEquality_GeneratesCorrectWhereClause()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Id == 5);
        var sql = builder.Build();

        Assert.Contains("WHERE", sql);
        Assert.Contains("id = @param0", sql);
        var parameters = builder.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(5, parameters[0].Value);
    }

    [Fact]
    public void Where_WithNotEqual_GeneratesCorrectOperator()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Id != 10);
        var sql = builder.Build();

        Assert.Contains("id != @param0", sql);
        Assert.Equal(10, builder.GetParameters()[0].Value);
    }

    [Fact]
    public void Where_WithGreaterThan_GeneratesCorrectOperator()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Age > 18);
        var sql = builder.Build();

        Assert.Contains("age > @param0", sql);
        Assert.Equal(18, builder.GetParameters()[0].Value);
    }

    [Fact]
    public void Where_WithGreaterThanOrEqual_GeneratesCorrectOperator()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Age >= 21);
        var sql = builder.Build();

        Assert.Contains("age >= @param0", sql);
        Assert.Equal(21, builder.GetParameters()[0].Value);
    }

    [Fact]
    public void Where_WithLessThan_GeneratesCorrectOperator()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Age < 65);
        var sql = builder.Build();

        Assert.Contains("age < @param0", sql);
        Assert.Equal(65, builder.GetParameters()[0].Value);
    }

    [Fact]
    public void Where_WithLessThanOrEqual_GeneratesCorrectOperator()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Age <= 100);
        var sql = builder.Build();

        Assert.Contains("age <= @param0", sql);
        Assert.Equal(100, builder.GetParameters()[0].Value);
    }

    [Fact]
    public void Where_WithAndCondition_CombinesConditionsWithAnd()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Age > 18 && e.Age < 65);
        var sql = builder.Build();

        Assert.Contains("age > @param0", sql);
        Assert.Contains("AND", sql);
        Assert.Contains("age < @param1", sql);
        Assert.Equal(2, builder.GetParameters().Count);
    }

    [Fact]
    public void Where_WithOrCondition_CombinesConditionsWithOr()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Id == 1 || e.Id == 2);
        var sql = builder.Build();

        Assert.Contains("id = @param0", sql);
        Assert.Contains("OR", sql);
        Assert.Contains("id = @param1", sql);
    }

    [Fact]
    public void Where_WithMultipleCalls_CombinesWithAnd()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Age > 18).Where(e => e.Name == "John");
        var sql = builder.Build();

        Assert.Contains("WHERE", sql);
        Assert.Contains("age > @param0", sql);
        Assert.Contains("AND", sql);
        Assert.Contains("name = @param1", sql);
        Assert.Equal(2, builder.GetParameters().Count);
    }

    [Fact]
    public void Where_WithStringComparison_GeneratesCorrectParameter()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Name == "Alice");
        var sql = builder.Build();

        Assert.Contains("name = @param0", sql);
        Assert.Equal("Alice", builder.GetParameters()[0].Value);
    }

    [Fact]
    public void Where_WithCapturedVariable_UsesVariableValue()
    {
        var builder = new QueryBuilder<QueryTestEntity>();
        var targetAge = 25;

        builder.Where(e => e.Age == targetAge);
        var sql = builder.Build();

        Assert.Contains("age = @param0", sql);
        Assert.Equal(25, builder.GetParameters()[0].Value);
    }

    [Fact]
    public void Where_WithContainsMethod_GeneratesLikeClause()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Name.Contains("John"));
        var sql = builder.Build();

        Assert.Contains("name LIKE @param0", sql);
        Assert.Equal("John", builder.GetParameters()[0].Value);
    }

    [Fact]
    public void WhereRaw_WithCustomClause_InsertsRawSql()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.WhereRaw("age BETWEEN {0} AND 30", 18);
        var sql = builder.Build();

        Assert.Contains("WHERE age BETWEEN @param0 AND 30", sql);
        Assert.Equal(18, builder.GetParameters()[0].Value);
    }

    [Fact]
    public void WhereRaw_CombinedWithWhere_JoinsWithAnd()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Name == "John").WhereRaw("age > {0}", 20);
        var sql = builder.Build();

        Assert.Contains("name = @param0", sql);
        Assert.Contains("AND", sql);
        Assert.Contains("age > @param1", sql);
    }

    [Fact]
    public void OrderBy_WithSingleProperty_GeneratesOrderClause()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.OrderBy(e => e.Name);
        var sql = builder.Build();

        Assert.Contains("ORDER BY name ASC", sql);
    }

    [Fact]
    public void OrderByDescending_WithSingleProperty_GeneratesDescendingOrder()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.OrderByDescending(e => e.Age);
        var sql = builder.Build();

        Assert.Contains("ORDER BY age DESC", sql);
    }

    [Fact]
    public void OrderBy_WithMultipleCalls_CombinesOrderClauses()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.OrderBy(e => e.Name).OrderByDescending(e => e.Age);
        var sql = builder.Build();

        Assert.Contains("ORDER BY name ASC, age DESC", sql);
    }

    [Fact]
    public void Take_WithLimit_AddsLimitClause()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Take(10);
        var sql = builder.Build();

        Assert.Contains("LIMIT 10", sql);
    }

    [Fact]
    public void Skip_WithOffset_AddsOffsetClause()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Skip(5);
        var sql = builder.Build();

        Assert.Contains("OFFSET 5", sql);
    }

    [Fact]
    public void Take_AndSkip_CombinesPaginationClauses()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Take(20).Skip(10);
        var sql = builder.Build();

        Assert.Contains("LIMIT 20", sql);
        Assert.Contains("OFFSET 10", sql);
    }

    [Fact]
    public void Build_WithCompleteQuery_CombinesAllClauses()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder
            .Where(e => e.Age > 18)
            .OrderBy(e => e.Name)
            .Take(10)
            .Skip(5);
        var sql = builder.Build();

        Assert.Contains("SELECT id, name, age FROM public.test_entity", sql);
        Assert.Contains("WHERE", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT 10", sql);
        Assert.Contains("OFFSET 5", sql);
    }

    [Fact]
    public void Build_WithMultipleConditions_OrdersClausesCorrectly()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder
            .Where(e => e.Age > 18)
            .Where(e => e.Name == "John")
            .OrderByDescending(e => e.Age)
            .Take(5);
        var sql = builder.Build();

        var whereIndex = sql.IndexOf("WHERE", StringComparison.Ordinal);
        var orderIndex = sql.IndexOf("ORDER BY", StringComparison.Ordinal);
        var limitIndex = sql.IndexOf("LIMIT", StringComparison.Ordinal);

        Assert.True(whereIndex < orderIndex);
        Assert.True(orderIndex < limitIndex);
    }

    [Fact]
    public void GetParameters_ReturnsAllParameters()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Age > 18 && e.Name == "John");
        var parameters = builder.GetParameters();

        Assert.Equal(2, parameters.Count);
        Assert.Equal(18, parameters[0].Value);
        Assert.Equal("John", parameters[1].Value);
    }

    [Fact]
    public void GetParameters_WithMultipleWhereCalls_ReturnsAllParameters()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Age > 18).Where(e => e.Id == 5).Where(e => e.Name == "Test");
        var parameters = builder.GetParameters();

        Assert.Equal(3, parameters.Count);
        Assert.Equal(18, parameters[0].Value);
        Assert.Equal(5, parameters[1].Value);
        Assert.Equal("Test", parameters[2].Value);
    }

    [Fact]
    public void GetParameters_ParameterNamesAreUnique()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Age > 18 && e.Age < 65);
        var parameters = builder.GetParameters();

        Assert.Equal(2, parameters.Count);
        Assert.NotEqual(parameters[0].Name, parameters[1].Name);
        Assert.Equal("@param0", parameters[0].Name);
        Assert.Equal("@param1", parameters[1].Name);
    }

    [Fact]
    public void Where_WithNullValue_HandlesNull()
    {
        var builder = new QueryBuilder<QueryTestNullableEntity>();
        string? nullValue = null;

        builder.Where(e => e.Description == nullValue);
        var sql = builder.Build();

        Assert.Contains("description = @param0", sql);
        Assert.Equal(DBNull.Value, builder.GetParameters()[0].Value);
    }

    [Fact]
    public void Build_WithCustomSchema_UsesCorrectSchema()
    {
        var builder = new QueryBuilder<QueryTestCustomSchemaEntity>();

        var sql = builder.Build();

        Assert.Contains("SELECT id, name FROM custom_schema.custom_table", sql);
    }

    [Fact]
    public void OrderBy_WithConvertExpression_HandlesTypeConversion()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.OrderBy(e => e.Id);
        var sql = builder.Build();

        Assert.Contains("ORDER BY id ASC", sql);
    }

    [Fact]
    public void Where_WithComplexBooleanLogic_GeneratesNestedConditions()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => (e.Age > 18 && e.Age < 30) || e.Id == 1);
        var sql = builder.Build();

        Assert.Contains("((age > @param0) AND (age < @param1)) OR (id = @param2)", sql);
    }

    [Fact]
    public void Build_ChainingMethods_ReturnsBuilderInstance()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        var result = builder
            .Where(e => e.Age > 18)
            .OrderBy(e => e.Name)
            .Take(10)
            .Skip(5);

        Assert.Same(builder, result);
    }

    [Fact]
    public void Where_WithColumnAttribute_UsesColumnName()
    {
        var builder = new QueryBuilder<QueryTestColumnEntity>();

        builder.Where(e => e.FirstName == "John");
        var sql = builder.Build();

        Assert.Contains("first_name = @param0", sql);
    }

    [Fact]
    public void OrderBy_WithColumnAttribute_UsesColumnName()
    {
        var builder = new QueryBuilder<QueryTestColumnEntity>();

        builder.OrderBy(e => e.FirstName);
        var sql = builder.Build();

        Assert.Contains("ORDER BY first_name ASC", sql);
    }

    [Fact]
    public void Build_WithNoOrderOrPagination_DoesNotIncludeThoseClauses()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Where(e => e.Age > 18);
        var sql = builder.Build();

        Assert.DoesNotContain("ORDER BY", sql);
        Assert.DoesNotContain("LIMIT", sql);
        Assert.DoesNotContain("OFFSET", sql);
    }

    [Fact]
    public void Take_WithZero_AddsLimitZero()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Take(0);
        var sql = builder.Build();

        Assert.Contains("LIMIT 0", sql);
    }

    [Fact]
    public void Skip_WithZero_AddsOffsetZero()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Skip(0);
        var sql = builder.Build();

        Assert.Contains("OFFSET 0", sql);
    }

    [Fact]
    public void Where_WithMultipleCapturedVariables_UsesAllValues()
    {
        var builder = new QueryBuilder<QueryTestEntity>();
        var minAge = 18;
        var maxAge = 65;

        builder.Where(e => e.Age > minAge && e.Age < maxAge);
        var sql = builder.Build();

        var parameters = builder.GetParameters();
        Assert.Equal(2, parameters.Count);
        Assert.Equal(18, parameters[0].Value);
        Assert.Equal(65, parameters[1].Value);
    }

    [Fact]
    public void WhereRaw_WithMultiplePlaceholders_ReplacesOnlyFirstPlaceholder()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.WhereRaw("age = {0}", 25);
        var sql = builder.Build();

        Assert.Contains("WHERE age = @param0", sql);
        Assert.DoesNotContain("{0}", sql);
    }

    [Fact]
    public void Build_WithOnlyPagination_DoesNotIncludeWhereOrOrder()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.Take(10).Skip(5);
        var sql = builder.Build();

        Assert.DoesNotContain("WHERE", sql);
        Assert.DoesNotContain("ORDER BY", sql);
        Assert.Contains("LIMIT 10", sql);
        Assert.Contains("OFFSET 5", sql);
    }

    [Fact]
    public void Build_WithOnlyOrderBy_DoesNotIncludeWhereOrPagination()
    {
        var builder = new QueryBuilder<QueryTestEntity>();

        builder.OrderBy(e => e.Name);
        var sql = builder.Build();

        Assert.DoesNotContain("WHERE", sql);
        Assert.DoesNotContain("LIMIT", sql);
        Assert.DoesNotContain("OFFSET", sql);
        Assert.Contains("ORDER BY name ASC", sql);
    }
}

[Table("test_entity", Schema = "public")]
public class QueryTestEntity
{
    [PrimaryKey]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }
}

[Table("nullable_entity", Schema = "public")]
public class QueryTestNullableEntity
{
    [PrimaryKey]
    public int Id { get; set; }

    public string? Description { get; set; }
}

[Table("custom_table", Schema = "custom_schema")]
public class QueryTestCustomSchemaEntity
{
    [PrimaryKey]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[Table("column_entity", Schema = "public")]
public class QueryTestColumnEntity
{
    [PrimaryKey]
    public int Id { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;
}
