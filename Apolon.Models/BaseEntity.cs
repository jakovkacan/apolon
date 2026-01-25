using Apolon.Core.Attributes;

namespace Apolon.Models;

public abstract class BaseEntity
{
    [Required]
    [Column("id", DbType = "INT")]
    [PrimaryKey(AutoIncrement = true)]
    public int Id { get; set; }

    [Required]
    [Column("created_at",
        DbType = "TIMESTAMP",
        DefaultValue = "CURRENT_TIMESTAMP", DefaultIsRawSql = true)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at",
        DbType = "TIMESTAMP",
        DefaultValue = "CURRENT_TIMESTAMP", DefaultIsRawSql = true)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public override string ToString() =>
        $"{nameof(Id)}: {Id}, {nameof(CreatedAt)}: {CreatedAt}, {nameof(UpdatedAt)}: {UpdatedAt}";
}