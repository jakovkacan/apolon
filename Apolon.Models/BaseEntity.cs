using Apolon.Core.Attributes;

namespace Apolon.Models;

public abstract class BaseEntity
{
    [Column("id", DbType = "INT", IsNullable = false)]
    [PrimaryKey(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("created_at", 
        DbType = "TIMESTAMP", 
        DefaultValue = "CURRENT_TIMESTAMP", DefaultIsRawSql = true,
        IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at", 
        DbType = "TIMESTAMP", 
        DefaultValue = "CURRENT_TIMESTAMP", DefaultIsRawSql = true,
        IsNullable = false)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public override string ToString() =>
        $"{nameof(Id)}: {Id}, {nameof(CreatedAt)}: {CreatedAt}, {nameof(UpdatedAt)}: {UpdatedAt}";
}