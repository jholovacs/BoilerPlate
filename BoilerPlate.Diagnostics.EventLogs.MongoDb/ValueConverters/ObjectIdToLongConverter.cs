using MongoDB.Bson;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BoilerPlate.Diagnostics.EventLogs.MongoDb.ValueConverters;

/// <summary>
///     Converts MongoDB ObjectId to/from long for EventLogEntry.Id (Serilog logs use ObjectId _id).
///     Uses the first 8 bytes of the ObjectId (big-endian) to form a unique long.
/// </summary>
internal sealed class ObjectIdToLongConverter : ValueConverter<long, ObjectId>
{
    private static readonly ObjectIdToLongConverter Instance = new();

    private ObjectIdToLongConverter()
        : base(
            v => ObjectIdFromLong(v),
            v => LongFromObjectId(v))
    {
    }

    public static ObjectIdToLongConverter Default => Instance;

    private static ObjectId ObjectIdFromLong(long _)
    {
        // Read-only context: we don't write new logs; use empty ObjectId if ever needed
        return ObjectId.Empty;
    }

    private static long LongFromObjectId(ObjectId id) => ConvertToLong(id);

    /// <summary>
    ///     Converts ObjectId to long (for raw MongoDB queries).
    /// </summary>
    internal static long ConvertToLong(ObjectId id)
    {
        var bytes = id.ToByteArray();
        if (bytes.Length < 8) return 0;
        long value = 0;
        for (var i = 0; i < 8; i++)
            value = (value << 8) | bytes[i];
        return value;
    }
}
