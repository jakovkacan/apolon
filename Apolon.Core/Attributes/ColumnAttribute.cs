using System;

namespace Apolon.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public required string DbType { get; set; }
    public bool IsNullable { get; set; } = true;
    public object? DefaultValue { get; set; }
    public bool IsUnique { get; set; }
}