using Apolon.Core.Attributes;

namespace Apolon.Models;

[Table("checkup_type")]
public class CheckupType
{
    [Column("id", DbType = "INT", IsNullable = false)]
    [PrimaryKey(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("type_code", DbType = "VARCHAR(20)", IsUnique = true)]
    public string TypeCode { get; set; }

    public string Description { get; set; }

    // Seed data: GP, BLOOD, X-RAY, CT, MRI, ULTRA, EKG, ECHO, EYE, DERM, DENTA, MAMMO, EEG
}