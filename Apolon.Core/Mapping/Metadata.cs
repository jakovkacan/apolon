using System;
using System.Collections.Generic;
using System.Reflection;

namespace Apolon.Core.Mapping;

public class EntityMetadata
{
    public Type EntityType { get; set; }
    public string TableName { get; set; }
    public string Schema { get; set; }
    public List<Metadata> Columns { get; set; }
    public PrimaryKeyMetadata PrimaryKey { get; set; }
    public List<ForeignKeyMetadata> ForeignKeys { get; set; }
    public List<RelationshipMetadata> Relationships { get; set; }
}

public class Metadata
{
    public string PropertyName { get; set; }
    public string ColumnName { get; set; }
    public string DbType { get; set; }
    public bool IsNullable { get; set; }
    public object DefaultValue { get; set; }
    public bool IsUnique { get; set; }
    public PropertyInfo Property { get; set; }
}

public class PrimaryKeyMetadata
{
    public string PropertyName { get; set; }
    public string ColumnName { get; set; }
    public bool AutoIncrement { get; set; }
    public PropertyInfo Property { get; set; }
}

public class ForeignKeyMetadata
{
    public string PropertyName { get; set; }
    public string ColumnName { get; set; }
    public Type ReferencedTable { get; set; }
    public string ReferencedColumn { get; set; }
    public string OnDeleteBehavior { get; set; }
    public PropertyInfo Property { get; set; }
}

public class RelationshipMetadata
{
    public string PropertyName { get; set; }
    public Type RelatedType { get; set; }
    public RelationshipCardinality Cardinality { get; set; }
    public string ForeignKeyProperty { get; set; }
    public PropertyInfo Property { get; set; }
}

public enum RelationshipCardinality
{
    OneToOne,
    OneToMany,
    ManyToOne
}