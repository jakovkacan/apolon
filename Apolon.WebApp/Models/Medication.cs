using Apolon.Core.Attributes;
using Apolon.Models;

namespace Apolon.WebApp.Models;

[Table("medication")]
public class Medication : BaseEntity
{
    public required string Name { get; set; }
    public required string GenericName { get; set; }
    public required string DosageForm { get; set; }

    // Navigation properties
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    public override string ToString() =>
        $"{nameof(Name)}: {Name}, {nameof(GenericName)}: {GenericName}, {nameof(DosageForm)}: {DosageForm}, {nameof(Prescriptions)}: {Prescriptions}";
}