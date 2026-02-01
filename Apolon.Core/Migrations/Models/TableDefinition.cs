namespace Apolon.Core.Migrations.Models;

/// <summary>
/// Represents the complete definition of a database table including columns and constraints.
/// Internal model used by migration fluent API. Use TableSchema for public APIs.
/// </summary>
internal sealed record TableDefinition
{
    /// <summary>
    /// The schema name (e.g., "public", "dbo").
    /// </summary>
    public required string Schema { get; init; }

    /// <summary>
    /// The table name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// All columns in the table.
    /// </summary>
    public required IReadOnlyList<ColumnDefinition> Columns { get; init; }

    /// <summary>
    /// Primary key constraint definition.
    /// </summary>
    public PrimaryKeyConstraint? PrimaryKey { get; init; }

    /// <summary>
    /// Foreign key constraints.
    /// </summary>
    public IReadOnlyList<ForeignKeyConstraint> ForeignKeys { get; init; } = [];

    /// <summary>
    /// Unique constraints.
    /// </summary>
    public IReadOnlyList<UniqueConstraint> UniqueConstraints { get; init; } = [];

    /// <summary>
    /// Check constraints.
    /// </summary>
    public IReadOnlyList<CheckConstraint> CheckConstraints { get; init; } = [];

    /// <summary>
    /// Table comment/description.
    /// </summary>
    public string? Comment { get; init; }
}

/// <summary>
/// Primary key constraint definition.
/// Internal model used by migration fluent API. Use PrimaryKeySchema for public APIs.
/// </summary>
internal sealed record PrimaryKeyConstraint
{
    /// <summary>
    /// The constraint name (e.g., "PK_Customers").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Column names that form the primary key.
    /// </summary>
    public required IReadOnlyList<string> Columns { get; init; }
}

/// <summary>
/// Foreign key constraint definition.
/// Internal model used by migration fluent API. Use ForeignKeySchema for public APIs.
/// </summary>
internal sealed record ForeignKeyConstraint
{
    /// <summary>
    /// The constraint name (e.g., "FK_Orders_Customers").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Local column names.
    /// </summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>
    /// Referenced table schema.
    /// </summary>
    public string? PrincipalSchema { get; init; }

    /// <summary>
    /// Referenced table name.
    /// </summary>
    public required string PrincipalTable { get; init; }

    /// <summary>
    /// Referenced column names.
    /// </summary>
    public required IReadOnlyList<string> PrincipalColumns { get; init; }

    /// <summary>
    /// ON DELETE behavior (e.g., "CASCADE", "SET NULL", "NO ACTION").
    /// </summary>
    public string? OnDelete { get; init; }

    /// <summary>
    /// ON UPDATE behavior (e.g., "CASCADE", "SET NULL", "NO ACTION").
    /// </summary>
    public string? OnUpdate { get; init; }
}

/// <summary>
/// Unique constraint definition.
/// Internal model used by migration fluent API. Use UniqueConstraintSchema for public APIs.
/// </summary>
internal sealed record UniqueConstraint
{
    /// <summary>
    /// The constraint name (e.g., "UQ_Customers_Email").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Column names that form the unique constraint.
    /// </summary>
    public required IReadOnlyList<string> Columns { get; init; }
}

/// <summary>
/// Check constraint definition.
/// Internal model used by migration fluent API. Use CheckConstraintSchema for public APIs.
/// </summary>
internal sealed record CheckConstraint
{
    /// <summary>
    /// The constraint name (e.g., "CK_Products_Price").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The SQL expression for the check (e.g., "Price > 0").
    /// </summary>
    public required string Expression { get; init; }
}
