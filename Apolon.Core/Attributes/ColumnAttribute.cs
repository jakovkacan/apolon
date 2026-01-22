using System;

namespace Apolon.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public string? DbType { get; set; }
    public bool IsNullable { get; set; } = true;
    public object? DefaultValue { get; set; }
    public bool DefaultIsRawSql { get; set; } = false;
    public bool IsUnique { get; set; }
}