using Apolon.Core.Attributes;

namespace Apolon.Core.Migrations.Models;

[Table("__apolon_migrations", Schema = "apolon")]
public class MigrationHistoryTable
{
    [PrimaryKey]
    public int MigrationId { get; init; }
    [Column("migration_name", IsUnique = true)]
    public required string MigrationName { get; init; }
    public string? ProductVersion { get; set; }
    [Column("applied_at", DefaultIsRawSql = true, DefaultValue = "CURRENT_TIMESTAMP")]
    public DateTime AppliedAt { get; init; } = DateTime.UtcNow;
}