using System;

namespace Apolon.Core.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class TableAttribute(string name) : Attribute
{
    public string Name { get; set; } = name;
    public string Schema { get; set; } = "public";

    public TableAttribute(string name, string schema) : this(name) => (Schema) = (schema);
}