using Apolon.Core.Attributes;

namespace Apolon.Models;

[Table("prescription")]
public class Prescription : BaseEntity
{
    [Required]
    [Column("checkup_id", DbType = "INT")]
    [ForeignKey(typeof(Checkup), OnDeleteBehavior = OnDeleteBehavior.Cascade)]
    public int CheckupId { get; set; }

    [Required]
    [Column("medication_id", DbType = "INT")]
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