namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
/// Base interface for all messages in the service bus
/// Provides common properties for message tracking and processing
/// </summary>
public interface IMessage
{
    /// <summary>
    /// Trace ID for distributed tracing (correlation ID)
    /// </summary>
    string? TraceId { get; set; }

    /// <summary>
    /// Reference ID for linking related messages or operations
    /// </summary>
    string? ReferenceId { get; set; }

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    DateTime CreatedTimestamp { get; set; }

    /// <summary>
    /// Number of times the message processing has failed
    /// Used for retry logic and dead letter queue handling
    /// </summary>
    int FailureCount { get; set; }
}
