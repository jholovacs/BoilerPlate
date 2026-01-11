namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
///     Result of message processing
/// </summary>
public enum MessageProcessingResult
{
    /// <summary>
    ///     Message was processed successfully
    /// </summary>
    Success,

    /// <summary>
    ///     Message processing failed and should be retried
    /// </summary>
    Failed,

    /// <summary>
    ///     Message processing failed permanently and should not be retried
    /// </summary>
    PermanentFailure
}