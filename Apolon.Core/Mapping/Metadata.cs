using System.Reflection;

namespace Apolon.Core.Mapping;

internal class EntityMetadata
{
    public required Type EntityType { get; init; }
    public required string TableName { get; init; }
    public required string Schema { get; init; }
    public required List<Metadata> Columns { get; init; }
    public required PrimaryKeyMetadata PrimaryKey { get; init; }
    public required IReadOnlyList<ForeignKeyMetadata> ForeignKeys { get; init; }
    public required IReadOnlyList<RelationshipMetadata> Relationships { get; set; }
}

internal class Metadata
{
    public required string PropertyName { get; init; }
    public required string ColumnName { get; init; }
    public required string DbType { get; init; }
    public required bool IsNullable { get; init; }
    public object? DefaultValue { get; init; }
    public required bool IsUnique { get; init; }
    public required PropertyInfo Property { get; init; }
}

internal class PrimaryKeyMetadata
{
    public required string PropertyName { get; init; }
    public required string ColumnName { get; init; }
    public required bool AutoIncrement { get; init; }
    public required PropertyInfo Property { get; init; }
}

internal class ForeignKeyMetadata
{
    public required string PropertyName { get; init; }
    public required string ColumnName { get; init; }
    public required Type ReferencedTable { get; init; }
    public required string ReferencedColumn { get; init; }
    public required string OnDeleteBehavior { get; init; }
    public required PropertyInfo Property { get; init; }
}

internal class RelationshipMetadata
{
    public required string PropertyName { get; init; }
    public required Type RelatedType { get; init; }
    public required RelationshipCardinality Cardinality { get; init; }
    public string? ForeignKeyProperty { get; init; }
    public required PropertyInfo Property { get; init; }
}

internal enum RelationshipCardinality
{
    OneToOne,
    OneToMany,
    ManyToOne
}