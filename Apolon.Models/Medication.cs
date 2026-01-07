using Apolon.Core.Attributes;

namespace Apolon.Models;

[Table("medication")]
public class Medication : BaseEntity
{
    public string Name { get; set; }
    public string GenericName { get; set; }
    public string DosageForm { get; set; } // Tablet, Capsule, Liquid, etc.

    // Navigation properties
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}