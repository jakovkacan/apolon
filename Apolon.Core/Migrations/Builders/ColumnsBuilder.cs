namespace Apolon.Core.Migrations.Builders;

/// <summary>
///     Builder for defining columns within a CreateTable operation.
///     Used in the columns lambda: table => new { Id = table.Column&lt;int&gt;(), ... }
/// </summary>
public sealed class ColumnsBuilder
{
    /// <summary>
    ///     Defines a column with the specified CLR type.
    /// </summary>
    /// <typeparam name="T">The CLR type of the column (e.g., int, string, DateTime).</typeparam>
    /// <param name="type">Optional explicit SQL type (e.g., "VARCHAR(255)", "INT").</param>
    /// <param name="nullable">Whether the column allows NULL values. Defaults to true.</param>
    /// <param name="maxLength">Maximum length for string/binary types.</param>
    /// <param name="precision">Precision for numeric/decimal types.</param>
    /// <param name="scale">Scale for numeric/decimal types.</param>
    /// <param name="defaultValue">Default value as a CLR object.</param>
    /// <param name="defaultValueSql">Default value as SQL expression.</param>
    /// <param name="comment">Column comment/description.</param>
    /// <returns>A ColumnBuilder for further fluent configuration.</returns>
    public ColumnBuilder<T> Column<T>(
        string? type = null,
        bool? nullable = null,
        int? maxLength = null,
        int? precision = null,
        int? scale = null,
        object? defaultValue = null,
        string? defaultValueSql = null,
        string? comment = null)
    {
        // Create builder with a placeholder name (will be replaced by property name)
        var builder = new ColumnBuilder<T>("");

        if (type != null)
            builder.HasColumnType(type);

        if (nullable.HasValue)
            builder.IsNullable(nullable.Value);

        if (maxLength.HasValue)
            builder.HasMaxLength(maxLength.Value);

        if (precision.HasValue)
            builder.HasPrecision(precision.Value, scale);

        if (defaultValue != null)
            builder.HasDefaultValue(defaultValue);

        if (defaultValueSql != null)
            builder.HasDefaultValueSql(defaultValueSql);

        if (comment != null)
            builder.HasComment(comment);

        return builder;
    }
}