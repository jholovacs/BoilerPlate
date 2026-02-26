using BoilerPlate.ServiceBus.Abstractions;

namespace BoilerPlate.ServiceBus.RabbitMq.Tests;

internal class TestMessage : IMessage
{
    public string? TraceId { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public int FailureCount { get; set; }
    public string? Payload { get; set; }
}
