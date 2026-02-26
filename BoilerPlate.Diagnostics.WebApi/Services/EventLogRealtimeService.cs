using BoilerPlate.EventLogs.Abstractions;
using BoilerPlate.ServiceBus.Abstractions;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace BoilerPlate.Diagnostics.WebApi.Services;

/// <summary>
///     Subscribes to EventLogPublishedEvent topic and forwards to SignalR for real-time UI updates.
/// </summary>
public class EventLogRealtimeService : BackgroundService
{
    private readonly ITopicSubscriberFactory _subscriberFactory;
    private readonly IHubContext<Hubs.EventLogsHub> _hubContext;
    private readonly ILogger<EventLogRealtimeService> _logger;

    public EventLogRealtimeService(
        ITopicSubscriberFactory subscriberFactory,
        IHubContext<Hubs.EventLogsHub> hubContext,
        ILogger<EventLogRealtimeService> logger)
    {
        _subscriberFactory = subscriberFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting EventLogPublishedEvent topic subscriber for real-time SignalR");

        var subscriber = _subscriberFactory.CreateSubscriber<EventLogPublishedEvent>();
        await subscriber.SubscribeAsync(
            async (message, _, ct) =>
            {
                try
                {
                    var tenantId = Guid.TryParse(message.TenantId, out var t) ? t : ExtractTenantId(message.Properties);
                    var payload = new
                    {
                        message.Id,
                        message.Timestamp,
                        message.Level,
                        message.Source,
                        message.MessageTemplate,
                        message.Message,
                        message.TraceId,
                        message.SpanId,
                        message.TenantId,
                        message.Exception,
                        message.Properties
                    };

                    // Service admins get all; tenant admins get only their tenant's logs
                    await _hubContext.Clients.Group("service-admins").SendAsync("EventLog", payload, ct);
                    if (tenantId.HasValue)
                        await _hubContext.Clients.Group($"tenant:{tenantId.Value}").SendAsync("EventLog", payload, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error forwarding event log to SignalR");
                }
            },
            3,
            async (_, ex, _, _) => _logger.LogError(ex, "Permanently failed to process EventLogPublishedEvent"),
            stoppingToken);
    }

    private static Guid? ExtractTenantId(string? properties)
    {
        if (string.IsNullOrEmpty(properties)) return null;
        try
        {
            var doc = JsonDocument.Parse(properties);
            var root = doc.RootElement;
            foreach (var name in new[] { "tenantId", "TenantId" })
            {
                if (root.TryGetProperty(name, out var tid) && tid.ValueKind == JsonValueKind.String)
                {
                    var s = tid.GetString();
                    if (Guid.TryParse(s, out var g)) return g;
                }
            }
        }
        catch { /* ignore */ }
        return null;
    }
}
