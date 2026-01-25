using Apolon.Core.Migrations.Models;
using Xunit;

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

    [Fact]
    public void Diff_ReportsMissingTablesColumnsAndPropertyChanges()
    {
        var left = new SchemaSnapshot([
            new TableSnapshot("public", "users", [
                Col("id"),
                Col("status", columnDefault: "'active'")
            ])
        ]);

        var right = new SchemaSnapshot([
            new TableSnapshot("public", "users", [
                Col("id"),
                Col("status", columnDefault: null),
                Col("email")
            ]),
            new TableSnapshot("public", "roles", [
                Col("id")
            ])
        ]);

        var diffs = left.Diff(right);

        Assert.Contains("Table missing in THIS: public.roles", diffs);
        Assert.Contains("public.users: column missing in THIS: email", diffs);
        Assert.Contains("public.users.status: ColumnDefault differs (this='active', other=<null>)", diffs);
    }

    private static ColumnSnapshot Col(string name, string? columnDefault = null)
        => new(
            ColumnName: name,
            DataType: "int4",
            UdtName: "int4",
            CharacterMaximumLength: null,
            NumericPrecision: null,
            NumericScale: null,
            DateTimePrecision: null,
            IsNullable: false,
            ColumnDefault: columnDefault,
            IsIdentity: false,
            IdentityGeneration: null,
            IsGenerated: false,
            GenerationExpression: null,
            IsPrimaryKey: false,
            PkConstraintName: null,
            IsUnique: false,
            UniqueConstraintName: null,
            IsForeignKey: false,
            FkConstraintName: null,
            ReferencesSchema: null,
            ReferencesTable: null,
            ReferencesColumn: null,
            FkUpdateRule: null,
            FkDeleteRule: null
        );
}
