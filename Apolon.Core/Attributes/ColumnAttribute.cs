namespace Apolon.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public string? DbType { get; set; }
    public object? DefaultValue { get; set; }
    public bool DefaultIsRawSql { get; set; } = false;
}