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
