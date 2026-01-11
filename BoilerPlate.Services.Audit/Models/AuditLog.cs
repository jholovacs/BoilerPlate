using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BoilerPlate.Services.Audit.Models;

/// <summary>
///     Audit log entry stored in MongoDB
/// </summary>
public class AuditLog
{
    /// <summary>
    ///     MongoDB ObjectId (auto-generated)
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     Event type (e.g., "UserCreatedEvent", "UserModifiedEvent", "UserDeletedEvent", "UserDisabledEvent")
    /// </summary>
    [BsonElement("eventType")]
    public required string EventType { get; set; }

    /// <summary>
    ///     User ID associated with the event (UUID)
    /// </summary>
    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    /// <summary>
    ///     Tenant ID associated with the event (UUID)
    /// </summary>
    [BsonElement("tenantId")]
    [BsonRepresentation(BsonType.String)]
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Username at the time of the event
    /// </summary>
    [BsonElement("userName")]
    public string? UserName { get; set; }

    /// <summary>
    ///     Email address at the time of the event
    /// </summary>
    [BsonElement("email")]
    public string? Email { get; set; }

    /// <summary>
    ///     Full event data (serialized JSON)
    /// </summary>
    [BsonElement("eventData")]
    public required BsonDocument EventData { get; set; }

    /// <summary>
    ///     Trace ID for distributed tracing
    /// </summary>
    [BsonElement("traceId")]
    public string? TraceId { get; set; }

    /// <summary>
    ///     Reference ID for linking related operations
    /// </summary>
    [BsonElement("referenceId")]
    public string? ReferenceId { get; set; }

    /// <summary>
    ///     Timestamp when the event occurred (from the event)
    /// </summary>
    [BsonElement("eventTimestamp")]
    public DateTime EventTimestamp { get; set; }

    /// <summary>
    ///     Timestamp when the audit log was created
    /// </summary>
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Additional metadata about the event (from changed properties, roles, etc.)
    /// </summary>
    [BsonElement("metadata")]
    public BsonDocument? Metadata { get; set; }
}