namespace Apolon.Core.Migrations.Models;

/// <summary>
/// Represents the complete definition of a database column.
/// Internal model used by migration fluent API. Use ColumnSchema for public APIs.
/// </summary>
internal sealed record ColumnDefinition
{
    /// <summary>
    /// The name of the column.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The CLR type of the column (e.g., typeof(int), typeof(string)).
    /// </summary>
    public required Type ClrType { get; init; }

    /// <summary>
    /// The SQL type override (e.g., "VARCHAR(255)", "INT"). If null, will be inferred from ClrType.
    /// </summary>
    public string? SqlType { get; init; }

    /// <summary>
    /// Whether the column allows NULL values.
    /// </summary>
    public bool IsNullable { get; init; } = true;

    /// <summary>
    /// The default value SQL expression (e.g., "CURRENT_TIMESTAMP", "'default'").
    /// </summary>
    public string? DefaultValueSql { get; init; }

    /// <summary>
    /// The default value as a CLR object (will be converted to SQL).
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Maximum length for string/binary types.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Precision for numeric/decimal types.
    /// </summary>
    public int? Precision { get; init; }

    /// <summary>
    /// Scale for numeric/decimal types.
    /// </summary>
    public int? Scale { get; init; }

    /// <summary>
    /// Whether this column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; init; }

    /// <summary>
    /// Whether this column is an identity column (auto-increment).
    /// </summary>
    public bool IsIdentity { get; init; }

    /// <summary>
    /// Identity generation mode ("ALWAYS" or "BY DEFAULT").
    /// </summary>
    public string? IdentityGeneration { get; init; }

    /// <summary>
    /// Whether this column has a unique constraint.
    /// </summary>
    public bool IsUnique { get; init; }

    /// <summary>
    /// Provider-specific annotations
    /// </summary>
    public IReadOnlyDictionary<string, object>? Annotations { get; init; }

    /// <summary>
    /// Comment/description for the column.
    /// </summary>
    public string? Comment { get; init; }
}
