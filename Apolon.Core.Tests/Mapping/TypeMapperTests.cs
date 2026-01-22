using Apolon.Core.Exceptions;
using Apolon.Core.Mapping;

namespace Apolon.Core.Tests.Mapping;

public class TypeMapperTests
{
    [Fact]
    public void GetPostgresType_WithInt_ReturnsInt()
    {
        var result = TypeMapper.GetPostgresType(typeof(int));
        
        Assert.Equal("INT", result);
    }

    [Fact]
    public void GetPostgresType_WithLong_ReturnsBigInt()
    {
        var result = TypeMapper.GetPostgresType(typeof(long));
        
        Assert.Equal("BIGINT", result);
    }

    [Fact]
    public void GetPostgresType_WithShort_ReturnsSmallInt()
    {
        var result = TypeMapper.GetPostgresType(typeof(short));
        
        Assert.Equal("SMALLINT", result);
    }

    [Fact]
    public void GetPostgresType_WithDecimal_ReturnsDecimalWithPrecision()
    {
        var result = TypeMapper.GetPostgresType(typeof(decimal));
        
        Assert.Equal("DECIMAL(18,2)", result);
    }

    [Fact]
    public void GetPostgresType_WithFloat_ReturnsFloat()
    {
        var result = TypeMapper.GetPostgresType(typeof(float));
        
        Assert.Equal("FLOAT", result);
    }

    [Fact]
    public void GetPostgresType_WithDouble_ReturnsDoublePrecision()
    {
        var result = TypeMapper.GetPostgresType(typeof(double));
        
        Assert.Equal("DOUBLE PRECISION", result);
    }

    [Fact]
    public void GetPostgresType_WithString_ReturnsVarchar()
    {
        var result = TypeMapper.GetPostgresType(typeof(string));
        
        Assert.Equal("VARCHAR(255)", result);
    }

    [Fact]
    public void GetPostgresType_WithDateTime_ReturnsTimestamp()
    {
        var result = TypeMapper.GetPostgresType(typeof(DateTime));
        
        Assert.Equal("TIMESTAMP", result);
    }

    [Fact]
    public void GetPostgresType_WithDateTimeOffset_ReturnsTimestampWithTimeZone()
    {
        var result = TypeMapper.GetPostgresType(typeof(DateTimeOffset));
        
        Assert.Equal("TIMESTAMP WITH TIME ZONE", result);
    }

    [Fact]
    public void GetPostgresType_WithBool_ReturnsBoolean()
    {
        var result = TypeMapper.GetPostgresType(typeof(bool));
        
        Assert.Equal("BOOLEAN", result);
    }

    [Fact]
    public void GetPostgresType_WithGuid_ReturnsUuid()
    {
        var result = TypeMapper.GetPostgresType(typeof(Guid));
        
        Assert.Equal("UUID", result);
    }

    [Fact]
    public void GetPostgresType_WithByteArray_ReturnsBytea()
    {
        var result = TypeMapper.GetPostgresType(typeof(byte[]));
        
        Assert.Equal("BYTEA", result);
    }

    [Fact]
    public void GetPostgresType_WithNullableInt_ReturnsInt()
    {
        var result = TypeMapper.GetPostgresType(typeof(int?));
        
        Assert.Equal("INT", result);
    }

    [Fact]
    public void GetPostgresType_WithNullableDateTime_ReturnsTimestamp()
    {
        var result = TypeMapper.GetPostgresType(typeof(DateTime?));
        
        Assert.Equal("TIMESTAMP", result);
    }

    [Fact]
    public void GetPostgresType_WithUnsupportedType_ThrowsOrmException()
    {
        Assert.Throws<OrmException>(() => TypeMapper.GetPostgresType(typeof(object)));
    }

    [Fact]
    public void ConvertFromDb_WithNull_ReturnsNull()
    {
        var result = TypeMapper.ConvertFromDb(null, typeof(int));
        
        Assert.Null(result);
    }

    [Fact]
    public void ConvertFromDb_WithDBNull_ReturnsNull()
    {
        var result = TypeMapper.ConvertFromDb(DBNull.Value, typeof(int));
        
        Assert.Null(result);
    }

    [Fact]
    public void ConvertFromDb_WithDateTime_ReturnsDateTime()
    {
        var dateTime = new DateTime(2024, 1, 15);
        
        var result = TypeMapper.ConvertFromDb(dateTime, typeof(DateTime));
        
        Assert.Equal(dateTime, result);
    }

    [Fact]
    public void ConvertFromDb_WithBool_ReturnsBool()
    {
        var result = TypeMapper.ConvertFromDb(true, typeof(bool));
        
        Assert.Equal(true, result);
    }

    [Fact]
    public void ConvertFromDb_WithGuid_ReturnsGuid()
    {
        var guid = Guid.NewGuid();
        
        var result = TypeMapper.ConvertFromDb(guid, typeof(Guid));
        
        Assert.Equal(guid, result);
    }

    [Fact]
    public void ConvertFromDb_WithIntegerValue_ReturnsConvertedInt()
    {
        var result = TypeMapper.ConvertFromDb(42, typeof(int));
        
        Assert.Equal(42, result);
    }

    [Fact]
    public void ConvertFromDb_WithStringValue_ReturnsString()
    {
        var result = TypeMapper.ConvertFromDb("test", typeof(string));
        
        Assert.Equal("test", result);
    }

    [Fact]
    public void ConvertFromDb_WithNullableTargetType_ConvertsToUnderlyingType()
    {
        var result = TypeMapper.ConvertFromDb(42, typeof(int?));
        
        Assert.Equal(42, result);
    }

    [Fact]
    public void ConvertToDb_WithNull_ReturnsDBNull()
    {
        var result = TypeMapper.ConvertToDb(null);
        
        Assert.Equal(DBNull.Value, result);
    }

    [Fact]
    public void ConvertToDb_WithValue_ReturnsValue()
    {
        var result = TypeMapper.ConvertToDb(42);
        
        Assert.Equal(42, result);
    }

    [Fact]
    public void ConvertToDb_WithStringValue_ReturnsString()
    {
        var result = TypeMapper.ConvertToDb("test");
        
        Assert.Equal("test", result);
    }
}
