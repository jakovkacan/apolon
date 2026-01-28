using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations.Builders;

/// <summary>
/// Fluent builder for configuring a column definition.
/// </summary>
/// <typeparam name="T">The CLR type of the column.</typeparam>
public sealed class ColumnBuilder<T>
{
    private readonly string _name;
    private readonly Type _clrType;
    private string? _sqlType;
    private bool _isNullable = true;
    private string? _defaultValueSql;
    private object? _defaultValue;
    private int? _maxLength;
    private int? _precision;
    private int? _scale;
    private bool _isPrimaryKey;
    private bool _isIdentity;
    private string? _identityGeneration;
    private bool _isUnique;
    private Dictionary<string, object>? _annotations;
    private string? _comment;

    internal ColumnBuilder(string name)
    {
        _name = name;
        _clrType = typeof(T);

        // For nullable value types, make column nullable by default
        if (Nullable.GetUnderlyingType(typeof(T)) != null)
        {
            _isNullable = true;
        }
        // For reference types, check nullability context (simplified - always nullable by default for strings)
        else if (!typeof(T).IsValueType)
        {
            _isNullable = true;
        }
    }

    /// <summary>
    /// Sets the SQL type explicitly (e.g., "VARCHAR(255)", "INT").
    /// </summary>
    public ColumnBuilder<T> HasColumnType(string sqlType)
    {
        _sqlType = sqlType;
        return this;
    }

    /// <summary>
    /// Sets whether the column is nullable.
    /// </summary>
    public ColumnBuilder<T> IsNullable(bool nullable = true)
    {
        _isNullable = nullable;
        return this;
    }

    /// <summary>
    /// Sets whether the column is required (NOT NULL).
    /// </summary>
    public ColumnBuilder<T> IsRequired()
    {
        _isNullable = false;
        return this;
    }

    /// <summary>
    /// Sets a default value SQL expression (e.g., "CURRENT_TIMESTAMP", "0").
    /// </summary>
    public ColumnBuilder<T> HasDefaultValueSql(string sql)
    {
        _defaultValueSql = sql;
        return this;
    }

    /// <summary>
    /// Sets a default value from a CLR object (will be converted to SQL).
    /// </summary>
    public ColumnBuilder<T> HasDefaultValue(object value)
    {
        _defaultValue = value;
        return this;
    }

    /// <summary>
    /// Sets the maximum length for string/binary columns.
    /// </summary>
    public ColumnBuilder<T> HasMaxLength(int maxLength)
    {
        _maxLength = maxLength;
        return this;
    }

    /// <summary>
    /// Sets the precision for numeric/decimal columns.
    /// </summary>
    public ColumnBuilder<T> HasPrecision(int precision, int? scale = null)
    {
        _precision = precision;
        _scale = scale;
        return this;
    }

    /// <summary>
    /// Marks this column as part of the primary key.
    /// </summary>
    public ColumnBuilder<T> IsPrimaryKey()
    {
        _isPrimaryKey = true;
        return this;
    }

    /// <summary>
    /// Marks this column as an identity/auto-increment column.
    /// </summary>
    public ColumnBuilder<T> IsIdentity(string generation = "ALWAYS")
    {
        _isIdentity = true;
        _identityGeneration = generation;
        return this;
    }

    /// <summary>
    /// Marks this column as having a unique constraint.
    /// </summary>
    public ColumnBuilder<T> IsUnique()
    {
        _isUnique = true;
        return this;
    }

    /// <summary>
    /// Adds a provider-specific annotation (e.g., "SqlServer:Identity", "1, 1").
    /// </summary>
    public ColumnBuilder<T> Annotation(string name, object value)
    {
        _annotations ??= new Dictionary<string, object>();
        _annotations[name] = value;
        return this;
    }

    /// <summary>
    /// Sets a comment/description for the column.
    /// </summary>
    public ColumnBuilder<T> HasComment(string comment)
    {
        _comment = comment;
        return this;
    }

    /// <summary>
    /// Builds the final ColumnDefinition.
    /// </summary>
    internal ColumnDefinition Build(string name)
    {
        return new ColumnDefinition
        {
            Name = name,
            ClrType = _clrType,
            SqlType = _sqlType,
            IsNullable = _isNullable,
            DefaultValueSql = _defaultValueSql,
            DefaultValue = _defaultValue,
            MaxLength = _maxLength,
            Precision = _precision,
            Scale = _scale,
            IsPrimaryKey = _isPrimaryKey,
            IsIdentity = _isIdentity,
            IdentityGeneration = _identityGeneration,
            IsUnique = _isUnique,
            Annotations = _annotations,
            Comment = _comment
        };
    }
}
