using Apolon.Core.Attributes;

namespace Apolon.WebApp.Models;

[Table("checkup_type")]
public class CheckupType
{
    [Required]
    [Column("id", DbType = "INT")]
    [PrimaryKey(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("type_code", DbType = "VARCHAR(20)", IsUnique = true)]
    public required string TypeCode { get; set; }

    public string? Description { get; set; }

    // Seed data: GP, BLOOD, X-RAY, CT, MRI, ULTRA, EKG, ECHO, EYE, DERM, DENTA, MAMMO, EEG

    public override string ToString() =>
        $"{nameof(Id)}: {Id}, {nameof(TypeCode)}: {TypeCode}, {nameof(Description)}: {Description}";
}