using BoilerPlate.ServiceBus.Abstractions;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;

/// <summary>
/// Test message types for unit tests
/// </summary>
internal class UserCreatedEvent : IMessage
{
    public string? TraceId { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public int FailureCount { get; set; }
}

internal class Event : IMessage
{
    public string? TraceId { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public int FailureCount { get; set; }
}

internal class HTTPRequestEvent : IMessage
{
    public string? TraceId { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public int FailureCount { get; set; }
}

internal class GenericMessage<T> : IMessage
{
    public string? TraceId { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public int FailureCount { get; set; }
}

internal class TestMessage : IMessage
{
    public string? TraceId { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public int FailureCount { get; set; }
}
