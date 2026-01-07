using System;

namespace Apolon.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ForeignKeyAttribute(Type type, string referencedColumn) : Attribute
{
    public Type ReferencedTable { get; set; } = type;
    public string ReferencedColumn { get; set; } = referencedColumn;
    public string OnDeleteBehavior { get; set; } = "CASCADE";
}