using Apolon.Core.Attributes;

namespace Apolon.Core.Migrations;

[Table("__EFMigrationsHistory", Schema = "public")]
public class MigrationHistory
{
    [PrimaryKey]
    public string MigrationId { get; set; } = null!;
    public string ProductVersion { get; set; } = null!;
    public DateTime AppliedAt { get; set; }
}