using Apolon.Core.Mapping.Models;

namespace Apolon.Core.Sql.Models;

internal class TableSchema
{
    public required string Name { get; init; }
    public required string Schema { get; init; }
    public required IList<ColumnSchema> Columns { get; init; }
    public required PrimaryKeySchema PrimaryKey { get; init; }
    public required IList<ForeignKeySchema> ForeignKeys { get; init; }
    public required IList<RelationshipSchema> Relationships { get; init; }
}

internal class ColumnSchema
{
    public required string Name { get; init; }
    public required string DbType { get; init; }
    public required bool IsNullable { get; init; }
    public object? DefaultValue { get; init; }
    public required bool IsUnique { get; init; }
}

internal class PrimaryKeySchema
{
    public required string Name { get; init; }
    public required bool AutoIncrement { get; init; }
}

internal class ForeignKeySchema
{
    public required string Name { get; init; }
    public required string ReferencedTable { get; init; }
    public required string ReferencedColumn { get; init; }
    public required string OnDeleteBehavior { get; init; }
}

internal class RelationshipSchema
{
    public required string Name { get; init; }
    public required string RelatedTable { get; init; }
    public required RelationshipCardinality Cardinality { get; init; }
    public required string ForeignKeyProperty { get; init; }
}