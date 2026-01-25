using Apolon.Core.Attributes;

namespace Apolon.Models;

[Table("patient")]
public class Patient : BaseEntity
{
    [Required]
    [Column("first_name", DbType = "VARCHAR(100)")]
    public required string FirstName { get; set; }

    [Required]
    [Column("last_name", DbType = "VARCHAR(100)")]
    public required string LastName { get; set; }

    [Required]
    [Column("email", DbType = "VARCHAR(255)", IsUnique = true)]
    public required string Email { get; set; }

    [Optional]
    [Column("phone_number", DbType = "VARCHAR(20)")]
    public string? PhoneNumber { get; set; }

    [Optional]
    [Column("date_of_birth", DbType = "TIMESTAMP")]
    public DateTime? DateOfBirth { get; set; }

    [Optional]
    [Column("gender", DbType = "VARCHAR(10)")]
    public string? Gender { get; set; }

    [Optional]
    [Column("address", DbType = "VARCHAR(255)")]
    public string? Address { get; set; }

    // Navigation properties
    public ICollection<Checkup> Checkups { get; set; } = new List<Checkup>();

    public override string ToString() =>
        $"{nameof(FirstName)}: {FirstName}, {nameof(LastName)}: {LastName}, {nameof(Email)}: {Email}, {nameof(PhoneNumber)}: {PhoneNumber}, {nameof(DateOfBirth)}: {DateOfBirth}, {nameof(Gender)}: {Gender}, {nameof(Address)}: {Address}";
}