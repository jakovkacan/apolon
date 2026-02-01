using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Tests.Migrations;

public class SchemaSnapshotTests
{
    [Fact]
    public void Equals_IgnoresTableAndColumnOrdering()
    {
        var snapshotA = new SchemaSnapshot([
            new TableSnapshot("public", "users", [
                Col("id"),
                Col("name")
            ]),
            new TableSnapshot("public", "roles", [
                Col("id")
            ])
        ]);

        var snapshotB = new SchemaSnapshot([
            new TableSnapshot("public", "roles", [
                Col("id")
            ]),
            new TableSnapshot("public", "users", [
                Col("name"),
                Col("id")
            ])
        ]);

        Assert.True(snapshotA.Equals(snapshotB));
    }

    private static ColumnSnapshot Col(string name, string? columnDefault = null)
    {
        return new ColumnSnapshot(
            name,
            "int4",
            "int4",
            null,
            null,
            null,
            null,
            false,
            columnDefault,
            false,
            null,
            false,
            null,
            false,
            null,
            false,
            null,
            false,
            null,
            null,
            null,
            null,
            null,
            null
        );
    }
}