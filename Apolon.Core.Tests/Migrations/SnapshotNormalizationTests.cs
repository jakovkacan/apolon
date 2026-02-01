using Apolon.Core.Migrations;

namespace Apolon.Core.Tests.Migrations;

public class SnapshotNormalizationTests
{
    [Fact]
    public void NormalizeDataType_MapsCommonPostgresTypes()
    {
        Assert.Equal("varchar", SnapshotNormalization.NormalizeDataType("character varying(255)"));
        Assert.Equal("timestamptz", SnapshotNormalization.NormalizeDataType("timestamp with time zone"));
        Assert.Equal("double", SnapshotNormalization.NormalizeDataType("DOUBLE   precision"));
    }

    [Fact]
    public void ExtractDataTypeDetails_ParsesCommonShapes()
    {
        var varchar = SnapshotNormalization.ExtractDataTypeDetails("varchar(50)");
        Assert.Equal(50, varchar.CharacterMaximumLength);
        Assert.Null(varchar.NumericPrecision);
        Assert.Null(varchar.NumericScale);

        var int4 = SnapshotNormalization.ExtractDataTypeDetails("int4");
        Assert.Null(int4.CharacterMaximumLength);
        Assert.Equal(32, int4.NumericPrecision);
        Assert.Equal(0, int4.NumericScale);
    }

    [Fact]
    public void NormalizeDefault_StripsCastsParensAndNormalizesNow()
    {
        Assert.Equal("'abc'", SnapshotNormalization.NormalizeDefault("(('abc'::text))"));
        Assert.Equal("current_timestamp", SnapshotNormalization.NormalizeDefault("((now()))"));
        Assert.Null(SnapshotNormalization.NormalizeDefault("   "));
    }
}