using MongoDB.Bson;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BoilerPlate.Diagnostics.AuditLogs.MongoDb.ValueConverters;

/// <summary>
///     Converts BsonDocument (MongoDB audit_logs eventData/metadata) to/from JSON string for AuditLogEntry.
/// </summary>
internal sealed class BsonDocumentToStringConverter : ValueConverter<string?, BsonDocument?>
{
    private static readonly BsonDocumentToStringConverter Instance = new();

    private BsonDocumentToStringConverter()
        : base(
            v => string.IsNullOrEmpty(v) ? null : BsonDocument.Parse(v),
            v => ToJson(v))
    {
    }

    private static string ToJson(BsonDocument? v) => v == null ? "{}" : v.ToJson();

    public static BsonDocumentToStringConverter Default => Instance;
}
