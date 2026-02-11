namespace BoilerPlate.Diagnostics.Database.Entities;

/// <summary>
///     Represents an audit log entry in the audit logs store (e.g. user/tenant audit events in MongoDB).
/// </summary>
public class AuditLogEntry
{
    /// <summary>
    ///     Primary key (e.g. MongoDB ObjectId as string or SQL identity).
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    ///     Event type (e.g. UserCreatedEvent, UserModifiedEvent).
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    ///     User ID associated with the event.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    ///     Tenant ID associated with the event.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Username at the time of the event.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    ///     Email at the time of the event.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    ///     Full event data as JSON.
    /// </summary>
    public string EventData { get; set; } = null!;

    /// <summary>
    ///     Trace ID for distributed tracing.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    ///     Reference ID for linking related operations.
    /// </summary>
    public string? ReferenceId { get; set; }

    /// <summary>
    ///     Timestamp when the event occurred.
    /// </summary>
    public DateTime EventTimestamp { get; set; }

    /// <summary>
    ///     Timestamp when the audit log was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Additional metadata as JSON.
    /// </summary>
    public string? Metadata { get; set; }
}
