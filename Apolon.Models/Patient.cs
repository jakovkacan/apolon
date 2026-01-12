using Apolon.Core.Attributes;

namespace Apolon.Models;

[Table("patient")]
public class Patient : BaseEntity
{
    [Column("first_name", DbType = "VARCHAR(100)", IsNullable = false)]
    public string FirstName { get; set; }

    [Column("last_name", DbType = "VARCHAR(100)", IsNullable = false)]
    public string LastName { get; set; }

    [Column("email", DbType = "VARCHAR(255)", IsUnique = true, IsNullable = false)]
    public string Email { get; set; }

    [Column("phone_number", DbType = "VARCHAR(20)", IsNullable = true)]
    public string? PhoneNumber { get; set; }

    [Column("date_of_birth", DbType = "DATETIME", IsNullable = true)]
    public DateTime? DateOfBirth { get; set; }

    [Column("gender", DbType = "VARCHAR(10)", IsNullable = true)]
    public string? Gender { get; set; }

    [Column("address", DbType = "VARCHAR(255)", IsNullable = true)]
    public string? Address { get; set; }

    // Navigation properties
    public ICollection<Checkup> Checkups { get; set; } = new List<Checkup>();
}
