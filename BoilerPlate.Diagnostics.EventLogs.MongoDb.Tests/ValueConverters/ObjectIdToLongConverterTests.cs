using BoilerPlate.Diagnostics.EventLogs.MongoDb.ValueConverters;
using FluentAssertions;
using MongoDB.Bson;

namespace BoilerPlate.Diagnostics.EventLogs.MongoDb.Tests.ValueConverters;

/// <summary>
///     Unit tests for <see cref="ObjectIdToLongConverter" />.
/// </summary>
public class ObjectIdToLongConverterTests
{
    /// <summary>
    ///     Scenario: ConvertToLong is called with ObjectId.Empty.
    ///     Expected: Returns 0 (empty ObjectId has all zero bytes).
    /// </summary>
    [Fact]
    public void ConvertToLong_WithEmptyObjectId_ReturnsZero()
    {
        var result = ObjectIdToLongConverter.ConvertToLong(ObjectId.Empty);

        result.Should().Be(0L);
    }

    /// <summary>
    ///     Scenario: ConvertToLong is called with an ObjectId that has known bytes.
    ///     Expected: Returns the big-endian long formed from the first 8 bytes.
    /// </summary>
    [Fact]
    public void ConvertToLong_WithKnownObjectId_ReturnsCorrectLong()
    {
        if (!ObjectId.TryParse("507f1f77bcf86cd799439011", out var oid))
            throw new InvalidOperationException("Invalid ObjectId");

        var result = ObjectIdToLongConverter.ConvertToLong(oid);

        var bytes = oid.ToByteArray();
        long expected = 0;
        for (var i = 0; i < 8; i++)
            expected = (expected << 8) | bytes[i];
        result.Should().Be(expected);
    }

    /// <summary>
    ///     Scenario: ConvertToLong is called with an ObjectId; the result is used to reconstruct ObjectId via EventLogsRawQueryService pattern.
    ///     Expected: The conversion is reversible for the first 8 bytes (big-endian long).
    /// </summary>
    [Fact]
    public void ConvertToLong_RoundTrip_First8BytesPreserved()
    {
        var oid = ObjectId.GenerateNewId();
        var longVal = ObjectIdToLongConverter.ConvertToLong(oid);

        var bytes = oid.ToByteArray();
        long expected = 0;
        for (var i = 0; i < 8; i++)
            expected = (expected << 8) | bytes[i];
        longVal.Should().Be(expected);
    }

    /// <summary>
    ///     Scenario: ConvertToLong is called with ObjectId having max value in first byte.
    ///     Expected: Returns correct positive long value.
    /// </summary>
    [Fact]
    public void ConvertToLong_WithMaxFirstByte_ReturnsCorrectValue()
    {
        var bytes = new byte[12];
        bytes[0] = 0xFF;
        bytes[1] = 0xFF;
        bytes[2] = 0xFF;
        bytes[3] = 0xFF;
        bytes[4] = 0xFF;
        bytes[5] = 0xFF;
        bytes[6] = 0xFF;
        bytes[7] = 0xFF;
        var oid = new ObjectId(bytes);

        var result = ObjectIdToLongConverter.ConvertToLong(oid);

        result.Should().Be(-1L);
    }

    /// <summary>
    ///     Scenario: ConvertToLong is called with ObjectId having minimal non-zero value.
    ///     Expected: Returns 1 (only last byte set).
    /// </summary>
    [Fact]
    public void ConvertToLong_WithMinimalNonZero_ReturnsOne()
    {
        var bytes = new byte[12];
        bytes[7] = 1;
        var oid = new ObjectId(bytes);

        var result = ObjectIdToLongConverter.ConvertToLong(oid);

        result.Should().Be(1L);
    }
}
