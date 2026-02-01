using Apolon.Core.Attributes;

namespace Apolon.Models;

[Table("test", Schema = "internal")]
public class Test
{
    [Required]
    [PrimaryKey(AutoIncrement = true)]
    public int Id { get; set; }

    [Required]
    [ForeignKey(typeof(Checkup))]
    public int CheckupId { get; set; }

    [Required] public required string Name { get; set; }

    [Required]
    [Unique]
    [Column("serial_number", DbType = "VARCHAR(20)")]
    public required string SerialNumber { get; set; }

    [Column("description", DbType = "VARCHAR(100)")]
    public string? Description { get; set; }

    [Column("created_at", DbType = "TIMESTAMP", DefaultValue = "CURRENT_TIMESTAMP", DefaultIsRawSql = true)]
    public required DateTime CreatedAt { get; set; }

    public required float PreciseResult { get; set; }

    public decimal? ApproximateResult { get; set; }

    [NotMapped] public string? InternalNotes { get; set; }
}