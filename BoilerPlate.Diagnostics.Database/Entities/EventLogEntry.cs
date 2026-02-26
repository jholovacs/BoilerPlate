namespace BoilerPlate.Diagnostics.Database.Entities;

/// <summary>
///     Represents an event/log entry in the event logs store (e.g. application logs in MongoDB).
/// </summary>
public class EventLogEntry
{
    /// <summary>
    ///     Primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    ///     UTC timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    ///     Log level (e.g. Information, Warning, Error).
    /// </summary>
    public string Level { get; set; } = null!;

    /// <summary>
    ///     Application or source name that produced the log.
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
    ///     OpenTelemetry trace ID for distributed tracing.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    ///     OpenTelemetry span ID.
    /// </summary>
    public string? SpanId { get; set; }

    /// <summary>
    ///     Exception type or message if the log represents an error.
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    ///     Additional properties as JSON (e.g. structured log properties).
    /// </summary>
    public string? Properties { get; set; }
}
