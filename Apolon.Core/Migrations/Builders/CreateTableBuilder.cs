using System.Linq.Expressions;
using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations.Builders;

/// <summary>
///     Builder for defining table constraints within a CreateTable operation.
/// </summary>
/// <typeparam name="TColumns">Anonymous type representing the columns.</typeparam>
public sealed class CreateTableBuilder<TColumns>
{
    private readonly List<CheckConstraint> _checkConstraints = [];
    private readonly List<ForeignKeyConstraint> _foreignKeys = [];
    private readonly TableDefinition _tableDefinition;
    private readonly List<UniqueConstraint> _uniqueConstraints = [];
    private PrimaryKeyConstraint? _primaryKey;

    internal CreateTableBuilder(TableDefinition tableDefinition)
    {
        _tableDefinition = tableDefinition;
    }

    /// <summary>
    ///     Defines a primary key constraint.
    /// </summary>
    /// <param name="name">The constraint name (e.g., "PK_Customers").</param>
    /// <param name="columns">Expression selecting the column(s) for the primary key.</param>
    public CreateTableBuilder<TColumns> PrimaryKey(
        string name,
        Expression<Func<TColumns, object>> columns)
    {
        var columnNames = ExtractColumnNames(columns);
        _primaryKey = new PrimaryKeyConstraint
        {
            Name = name,
            Columns = columnNames
        };
        return this;
    }

    /// <summary>
    ///     Defines a foreign key constraint.
    /// </summary>
    /// <param name="name">The constraint name (e.g., "FK_Orders_Customers").</param>
    /// <param name="columns">Expression selecting the local column(s).</param>
    /// <param name="principalTable">The referenced table name.</param>
    /// <param name="principalColumns">The referenced column name(s).</param>
    /// <param name="principalSchema">The referenced table schema (optional).</param>
    /// <param name="onDelete">ON DELETE behavior (e.g., "CASCADE", "SET NULL").</param>
    /// <param name="onUpdate">ON UPDATE behavior (e.g., "CASCADE", "SET NULL").</param>
    public CreateTableBuilder<TColumns> ForeignKey(
        string name,
        Expression<Func<TColumns, object>> columns,
        string principalTable,
        string principalColumns,
        string? principalSchema = null,
        string? onDelete = null,
        string? onUpdate = null)
    {
        var columnNames = ExtractColumnNames(columns);
        _foreignKeys.Add(new ForeignKeyConstraint
        {
            Name = name,
            Columns = columnNames,
            PrincipalSchema = principalSchema,
            PrincipalTable = principalTable,
            PrincipalColumns = [principalColumns],
            OnDelete = onDelete,
            OnUpdate = onUpdate
        });
        return this;
    }

    /// <summary>
    ///     Defines a unique constraint.
    /// </summary>
    /// <param name="name">The constraint name (e.g., "UQ_Customers_Email").</param>
    /// <param name="columns">Expression selecting the column(s) for the unique constraint.</param>
    public CreateTableBuilder<TColumns> UniqueConstraint(
        string name,
        Expression<Func<TColumns, object>> columns)
    {
        var columnNames = ExtractColumnNames(columns);
        _uniqueConstraints.Add(new UniqueConstraint
        {
            Name = name,
            Columns = columnNames
        });
        return this;
    }

    /// <summary>
    ///     Defines a check constraint.
    /// </summary>
    /// <param name="name">The constraint name (e.g., "CK_Products_Price").</param>
    /// <param name="expression">The SQL expression for the check (e.g., "Price > 0").</param>
    public CreateTableBuilder<TColumns> CheckConstraint(
        string name,
        string expression)
    {
        _checkConstraints.Add(new CheckConstraint
        {
            Name = name,
            Expression = expression
        });
        return this;
    }

    /// <summary>
    ///     Builds the final TableDefinition with all constraints.
    /// </summary>
    internal TableDefinition Build()
    {
        return _tableDefinition with
        {
            PrimaryKey = _primaryKey,
            ForeignKeys = _foreignKeys,
            UniqueConstraints = _uniqueConstraints,
            CheckConstraints = _checkConstraints
        };
    }

    /// <summary>
    ///     Extracts column names from a lambda expression (e.g., x => x.Id or x => new { x.Id, x.Name }).
    /// </summary>
    private static List<string> ExtractColumnNames(Expression<Func<TColumns, object>> expression)
    {
        var columnNames = new List<string>();

        // Handle: x => x.PropertyName
        if (expression.Body is MemberExpression memberExpr)
        {
            columnNames.Add(memberExpr.Member.Name);
        }
        // Handle: x => new { x.Prop1, x.Prop2 }
        else if (expression.Body is NewExpression newExpr)
        {
            foreach (var arg in newExpr.Arguments)
                if (arg is MemberExpression member)
                    columnNames.Add(member.Member.Name);
        }
        // Handle: x => (object)x.PropertyName (conversion)
        else if (expression.Body is UnaryExpression { NodeType: ExpressionType.Convert } unaryExpr &&
                 unaryExpr.Operand is MemberExpression convertedMember)
        {
            columnNames.Add(convertedMember.Member.Name);
        }

        return columnNames;
    }
}