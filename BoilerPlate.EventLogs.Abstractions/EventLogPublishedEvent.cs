using BoilerPlate.ServiceBus.Abstractions;

namespace BoilerPlate.EventLogs.Abstractions;

/// <summary>
///     Event published when an event log is written to MongoDB.
///     Diagnostics API subscribes to this topic and forwards to SignalR for real-time UI updates.
/// </summary>
public class EventLogPublishedEvent : IMessage
{
    /// <summary>
    ///     MongoDB document ID (ObjectId as string) or generated ID.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    ///     UTC timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    ///     Log level (e.g. Information, Warning, Error).
    /// </summary>
    public string Level { get; set; } = null!;

    /// <summary>
    ///     Application or source name.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    ///     Serilog message template (e.g. "User {UserId} in tenant {TenantId}").
    /// </summary>
    public string? MessageTemplate { get; set; }

    /// <summary>
    ///     Rendered log message.
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    ///     Trace ID for distributed tracing.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    ///     Span ID.
    /// </summary>
    public string? SpanId { get; set; }

    /// <summary>
    ///     Tenant ID (top-level for filtering by tenant administrators).
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    ///     Exception details if applicable.
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    ///     Additional properties as JSON (includes tenantId when present).
    /// </summary>
    public string? Properties { get; set; }

    /// <inheritdoc />
    string? IMessage.TraceId { get => TraceId; set => TraceId = value; }

    /// <inheritdoc />
    public string? ReferenceId { get; set; }

    /// <inheritdoc />
    public DateTime CreatedTimestamp { get; set; }

    /// <inheritdoc />
    public int FailureCount { get; set; }
}

