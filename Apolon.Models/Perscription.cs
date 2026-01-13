using Apolon.Core.Attributes;

namespace Apolon.Models;

[Table("prescription")]
public class Prescription : BaseEntity
{
    [Column("checkup_id", DbType = "INT", IsNullable = false)]
    [ForeignKey(typeof(Checkup), "id", OnDeleteBehavior = "CASCADE")]
    public int CheckupId { get; set; }

    [Column("medication_id", DbType = "INT", IsNullable = false)]
    [ForeignKey(typeof(Medication), "id")]
    public int MedicationId { get; set; }

    public decimal Dosage { get; set; }
    public string DosageUnit { get; set; } // mg, ml, tablet, etc.
    public string Frequency { get; set; } // Once daily, twice daily, etc.
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; } // Optional - null means ongoing

    // Navigation properties
    public Checkup Checkup { get; set; }
    public Medication Medication { get; set; }

    public override string ToString() =>
        $"{nameof(CheckupId)}: {CheckupId}, {nameof(MedicationId)}: {MedicationId}, {nameof(Dosage)}: {Dosage}, {nameof(DosageUnit)}: {DosageUnit}, {nameof(Frequency)}: {Frequency}, {nameof(StartDate)}: {StartDate}, {nameof(EndDate)}: {EndDate}";
}