using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Tests.Migrations;

public class MigrationRunnerTests
{
    [Fact]
    public void DetermineMigrationsToRun_NoAppliedMigrations_ReturnsAllMigrations()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third")
        };
        var appliedMigrations = new List<string>();

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, null);

        Assert.Equal(3, result.Count);
        Assert.Equal("20240101000000_First", result[0].FullName);
        Assert.Equal("20240102000000_Second", result[1].FullName);
        Assert.Equal("20240103000000_Third", result[2].FullName);
    }

    [Fact]
    public void DetermineMigrationsToRun_SomeAppliedMigrations_ReturnsOnlyUnappliedMigrations()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third")
        };
        var appliedMigrations = new List<string> { "20240101000000_First" };

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, null);

        Assert.Equal(2, result.Count);
        Assert.Equal("20240102000000_Second", result[0].FullName);
        Assert.Equal("20240103000000_Third", result[1].FullName);
    }

    [Fact]
    public void DetermineMigrationsToRun_AllMigrationsApplied_ReturnsEmpty()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second"
        };

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, null);

        Assert.Empty(result);
    }

    [Fact]
    public void DetermineMigrationsToRun_WithTargetMigrationByName_StopsAtTarget()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third")
        };
        var appliedMigrations = new List<string>();

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, "Second");

        Assert.Equal(2, result.Count);
        Assert.Equal("20240101000000_First", result[0].FullName);
        Assert.Equal("20240102000000_Second", result[1].FullName);
    }

    [Fact]
    public void DetermineMigrationsToRun_WithTargetMigrationByFullName_StopsAtTarget()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third")
        };
        var appliedMigrations = new List<string>();

        var result =
            MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, "20240102000000_Second");

        Assert.Equal(2, result.Count);
        Assert.Equal("20240101000000_First", result[0].FullName);
        Assert.Equal("20240102000000_Second", result[1].FullName);
    }

    [Fact]
    public void DetermineMigrationsToRun_WithTargetMigrationCaseInsensitive_StopsAtTarget()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third")
        };
        var appliedMigrations = new List<string>();

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, "SECOND");

        Assert.Equal(2, result.Count);
        Assert.Equal("20240102000000_Second", result[1].FullName);
    }

    [Fact]
    public void DetermineMigrationsToRun_TargetAlreadyAppliedWithSubsequentUnapplied_IncludesUnappliedAfterTarget()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second"
        };

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, "Second");

        var migration = Assert.Single(result);
        Assert.Equal("20240103000000_Third", migration.FullName);
    }

    [Fact]
    public void DetermineMigrationsToRun_EmptyMigrationsList_ReturnsEmpty()
    {
        var allMigrations = Array.Empty<(Type, string, string)>();
        var appliedMigrations = new List<string>();

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, null);

        Assert.Empty(result);
    }

    [Fact]
    public void GetMigrationsToRollback_TargetFound_ReturnsCorrectMigrations()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third"),
            (Type: typeof(TestMigration4), Timestamp: "20240104000000", Name: "Fourth")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second",
            "20240103000000_Third",
            "20240104000000_Fourth"
        };

        var result = MigrationRunner.GetMigrationsToRollback(allMigrations, appliedMigrations, "Second");

        Assert.Equal(2, result.Count);
        Assert.Equal("20240104000000_Fourth", result[0]);
        Assert.Equal("20240103000000_Third", result[1]);
    }

    [Fact]
    public void GetMigrationsToRollback_TargetByFullName_ReturnsCorrectMigrations()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second",
            "20240103000000_Third"
        };

        var result = MigrationRunner.GetMigrationsToRollback(allMigrations, appliedMigrations, "20240101000000_First");

        Assert.Equal(2, result.Count);
        Assert.Equal("20240103000000_Third", result[0]);
        Assert.Equal("20240102000000_Second", result[1]);
    }

    [Fact]
    public void GetMigrationsToRollback_TargetCaseInsensitive_ReturnsCorrectMigrations()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second",
            "20240103000000_Third"
        };

        var result = MigrationRunner.GetMigrationsToRollback(allMigrations, appliedMigrations, "FIRST");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetMigrationsToRollback_TargetIsLatestMigration_ReturnsEmpty()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second",
            "20240103000000_Third"
        };

        var result = MigrationRunner.GetMigrationsToRollback(allMigrations, appliedMigrations, "Third");

        Assert.Empty(result);
    }

    [Fact]
    public void GetMigrationsToRollback_OnlyAppliedMigrationsIncluded_SkipsUnappliedMigrations()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third"),
            (Type: typeof(TestMigration4), Timestamp: "20240104000000", Name: "Fourth")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second",
            "20240104000000_Fourth"
        };

        var result = MigrationRunner.GetMigrationsToRollback(allMigrations, appliedMigrations, "First");

        Assert.Equal(2, result.Count);
        Assert.Equal("20240104000000_Fourth", result[0]);
        Assert.Equal("20240102000000_Second", result[1]);
        Assert.DoesNotContain("20240103000000_Third", result);
    }

    [Fact]
    public void GetMigrationsToRollback_ReturnsInDescendingTimestampOrder()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third"),
            (Type: typeof(TestMigration4), Timestamp: "20240104000000", Name: "Fourth")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second",
            "20240103000000_Third",
            "20240104000000_Fourth"
        };

        var result = MigrationRunner.GetMigrationsToRollback(allMigrations, appliedMigrations, "First");

        Assert.Equal(3, result.Count);
        Assert.Equal("20240104000000_Fourth", result[0]);
        Assert.Equal("20240103000000_Third", result[1]);
        Assert.Equal("20240102000000_Second", result[2]);
    }

    [Fact]
    public void GetMigrationsToRollback_TargetNotFound_ThrowsInvalidOperationException()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second"
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MigrationRunner.GetMigrationsToRollback(allMigrations, appliedMigrations, "NonExistent"));

        Assert.Contains("Target migration 'NonExistent' not found", exception.Message);
    }

    [Fact]
    public void GetMigrationsToRollback_EmptyAppliedMigrations_ReturnsEmpty()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second")
        };
        var appliedMigrations = new List<string>();

        var result = MigrationRunner.GetMigrationsToRollback(allMigrations, appliedMigrations, "First");

        Assert.Empty(result);
    }

    [Fact]
    public void DetermineMigrationsToRun_PreservesTypeInformation()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second")
        };
        var appliedMigrations = new List<string>();

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, null);

        Assert.Equal(typeof(TestMigration1), result[0].Type);
        Assert.Equal(typeof(TestMigration2), result[1].Type);
    }

    [Fact]
    public void DetermineMigrationsToRun_PreservesTimestampAndName()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First")
        };
        var appliedMigrations = new List<string>();

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, null);

        var migration = Assert.Single(result);
        Assert.Equal("20240101000000", migration.Timestamp);
        Assert.Equal("First", migration.Name);
        Assert.Equal("20240101000000_First", migration.FullName);
    }

    [Fact]
    public void DetermineMigrationsToRun_MigrationsInCorrectOrder_MaintainsOrder()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240103000000", Name: "Third"),
            (Type: typeof(TestMigration2), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration3), Timestamp: "20240102000000", Name: "Second")
        };
        var appliedMigrations = new List<string>();

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, null);

        Assert.Equal(3, result.Count);
        Assert.Equal("20240103000000_Third", result[0].FullName);
        Assert.Equal("20240101000000_First", result[1].FullName);
        Assert.Equal("20240102000000_Second", result[2].FullName);
    }

    [Fact]
    public void DetermineMigrationsToRun_WithPartiallyAppliedAndTarget_ReturnsCorrectSubset()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third"),
            (Type: typeof(TestMigration4), Timestamp: "20240104000000", Name: "Fourth"),
            (Type: typeof(TestMigration5), Timestamp: "20240105000000", Name: "Fifth")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second"
        };

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, "Fourth");

        Assert.Equal(2, result.Count);
        Assert.Equal("20240103000000_Third", result[0].FullName);
        Assert.Equal("20240104000000_Fourth", result[1].FullName);
    }

    [Fact]
    public void GetMigrationsToRollback_SingleMigrationToRollback_ReturnsSingleMigration()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second"
        };

        var result = MigrationRunner.GetMigrationsToRollback(allMigrations, appliedMigrations, "First");

        var migration = Assert.Single(result);
        Assert.Equal("20240102000000_Second", migration);
    }

    [Fact]
    public void DetermineMigrationsToRun_NullTargetMigration_ProcessesAllUnapplied()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second")
        };
        var appliedMigrations = new List<string> { "20240101000000_First" };

        var result = MigrationRunner.DetermineMigrationsToRun(allMigrations, appliedMigrations, null);

        var migration = Assert.Single(result);
        Assert.Equal("20240102000000_Second", migration.FullName);
    }

    [Fact]
    public void GetMigrationsToRollback_TargetIsFirstMigration_RollsBackAllOthers()
    {
        var allMigrations = new[]
        {
            (Type: typeof(TestMigration1), Timestamp: "20240101000000", Name: "First"),
            (Type: typeof(TestMigration2), Timestamp: "20240102000000", Name: "Second"),
            (Type: typeof(TestMigration3), Timestamp: "20240103000000", Name: "Third")
        };
        var appliedMigrations = new List<string>
        {
            "20240101000000_First",
            "20240102000000_Second",
            "20240103000000_Third"
        };

        var result = MigrationRunner.GetMigrationsToRollback(allMigrations, appliedMigrations, "First");

        Assert.Equal(2, result.Count);
        Assert.Contains("20240102000000_Second", result);
        Assert.Contains("20240103000000_Third", result);
    }
}

public class TestMigration1 : Migration
{
    public override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateSchema("test");
        migrationBuilder.CreateTable("test", "table1");
    }

    public override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("test", "table1");
    }
}

public class TestMigration2 : Migration
{
    public override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("test", "table2");
    }

    public override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("test", "table2");
    }
}

public class TestMigration3 : Migration
{
    public override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("test", "table3");
    }

    public override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("test", "table3");
    }
}

public class TestMigration4 : Migration
{
    public override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("test", "table4");
    }

    public override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("test", "table4");
    }
}

public class TestMigration5 : Migration
{
    public override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("test", "table5");
    }

    public override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("test", "table5");
    }
}