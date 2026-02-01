namespace Apolon.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ForeignKeyAttribute(Type type) : Attribute
{
    public ForeignKeyAttribute(Type type, string referencedColumn) : this(type)
    {
        ReferencedColumn = referencedColumn;
    }

    public Type ReferencedTable { get; set; } = type;
    public string? ReferencedColumn { get; set; }
    public OnDeleteBehavior OnDeleteBehavior { get; set; } = OnDeleteBehavior.NoAction;
}