using Apolon.Core.Attributes;

namespace Apolon.Models;

[Table("prescription")]
public class Prescription : BaseEntity
{
    [Column("checkup_id", DbType = "INT", IsNullable = false)]
    [ForeignKey(typeof(Checkup), OnDeleteBehavior = OnDeleteBehavior.Cascade)]
    public int CheckupId { get; set; }

    [Column("medication_id", DbType = "INT", IsNullable = false)]
    [ForeignKey(typeof(Medication))]
    public required int MedicationId { get; set; }

    public required decimal Dosage { get; set; }
    public required string DosageUnit { get; set; } 
    public required string Frequency { get; set; } 
    public required DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; } 

    // Navigation properties
    public Checkup? Checkup { get; set; }
    public Medication? Medication { get; set; }

    public override string ToString() =>
        $"{nameof(CheckupId)}: {CheckupId}, {nameof(MedicationId)}: {MedicationId}, {nameof(Dosage)}: {Dosage}, {nameof(DosageUnit)}: {DosageUnit}, {nameof(Frequency)}: {Frequency}, {nameof(StartDate)}: {StartDate}, {nameof(EndDate)}: {EndDate}";
}