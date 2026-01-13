using Apolon.Core.Attributes;

namespace Apolon.Models;

[Table("checkup")]
public class Checkup : BaseEntity
{
    [Column("patient_id", DbType = "INT", IsNullable = false)]
    [ForeignKey(typeof(Patient), "id", OnDeleteBehavior = "CASCADE")]
    public int PatientId { get; set; }

    [Column("checkup_type_id", DbType = "INT", IsNullable = false)]
    [ForeignKey(typeof(CheckupType), "id")]
    public int CheckupTypeId { get; set; }

    public DateTime CheckupDate { get; set; }
    public string Notes { get; set; }
    public string Results { get; set; }

    // Navigation properties
    public Patient Patient { get; set; }
    public CheckupType CheckupType { get; set; }
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    public override string ToString() =>
        $"{nameof(PatientId)}: {PatientId}, {nameof(CheckupTypeId)}: {CheckupTypeId}, {nameof(CheckupDate)}: {CheckupDate}, {nameof(Notes)}: {Notes}, {nameof(Results)}: {Results}";
}