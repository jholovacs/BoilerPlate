using BoilerPlate.Authentication.Services.Events;
using BoilerPlate.ServiceBus.Abstractions;
using MongoDB.Bson;

namespace BoilerPlate.Services.Audit.Services;

/// <summary>
///     Hosted service that subscribes to user events from queues and records them to MongoDB
/// </summary>
public class UserAuditEventSubscriber : BackgroundService
{
    private readonly AuditService _auditService;
    private readonly ILogger<UserAuditEventSubscriber> _logger;
    private readonly IQueueSubscriber<UserCreatedEvent> _userCreatedSubscriber;
    private readonly IQueueSubscriber<UserDeletedEvent> _userDeletedSubscriber;
    private readonly IQueueSubscriber<UserDisabledEvent> _userDisabledSubscriber;
    private readonly IQueueSubscriber<UserModifiedEvent> _userModifiedSubscriber;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserAuditEventSubscriber" /> class
    /// </summary>
    public UserAuditEventSubscriber(
        IQueueSubscriberFactory queueSubscriberFactory,
        AuditService auditService,
        ILogger<UserAuditEventSubscriber> logger)
    {
        _userCreatedSubscriber = queueSubscriberFactory.CreateSubscriber<UserCreatedEvent>();
        _userDeletedSubscriber = queueSubscriberFactory.CreateSubscriber<UserDeletedEvent>();
        _userDisabledSubscriber = queueSubscriberFactory.CreateSubscriber<UserDisabledEvent>();
        _userModifiedSubscriber = queueSubscriberFactory.CreateSubscriber<UserModifiedEvent>();
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    ///     Subscribes to all user event queues and processes messages
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting user audit event subscribers...");

        // Ensure indexes are created on startup
        try
        {
            await _auditService.EnsureIndexesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure indexes, continuing anyway");
        }

        // Subscribe to all event types in parallel
        var subscriptionTasks = new List<Task>
        {
            SubscribeToUserCreatedEvents(stoppingToken),
            SubscribeToUserDeletedEvents(stoppingToken),
            SubscribeToUserDisabledEvents(stoppingToken),
            SubscribeToUserModifiedEvents(stoppingToken)
        };

        try
        {
            await Task.WhenAll(subscriptionTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in user audit event subscribers");
            throw;
        }
    }

    private Task SubscribeToUserCreatedEvents(CancellationToken cancellationToken)
    {
        return _userCreatedSubscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                try
                {
                    var metadataDoc = metadata != null ? CreateBsonDocumentFromDictionary(metadata) : null;
                    await _auditService.RecordAuditLogAsync(
                        nameof(UserCreatedEvent),
                        message,
                        message.UserId,
                        message.TenantId,
                        message.UserName,
                        message.Email,
                        metadataDoc,
                        ct);

                    _logger.LogDebug("Recorded audit log for UserCreatedEvent: UserId={UserId}, TenantId={TenantId}",
                        message.UserId, message.TenantId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process UserCreatedEvent: UserId={UserId}, TenantId={TenantId}",
                        message.UserId, message.TenantId);
                    throw; // Re-throw to trigger retry logic
                }
            },
            3,
            async (message, ex, metadata, ct) =>
            {
                _logger.LogError(ex,
                    "Permanently failed to process UserCreatedEvent: UserId={UserId}, TenantId={TenantId}",
                    message.UserId, message.TenantId);
                await Task.CompletedTask;
            },
            cancellationToken);
    }

    private Task SubscribeToUserDeletedEvents(CancellationToken cancellationToken)
    {
        return _userDeletedSubscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                try
                {
                    var metadataDoc = metadata != null ? CreateBsonDocumentFromDictionary(metadata) : null;
                    await _auditService.RecordAuditLogAsync(
                        nameof(UserDeletedEvent),
                        message,
                        message.UserId,
                        message.TenantId,
                        message.UserName,
                        message.Email,
                        metadataDoc,
                        ct);

                    _logger.LogDebug("Recorded audit log for UserDeletedEvent: UserId={UserId}, TenantId={TenantId}",
                        message.UserId, message.TenantId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process UserDeletedEvent: UserId={UserId}, TenantId={TenantId}",
                        message.UserId, message.TenantId);
                    throw;
                }
            },
            3,
            async (message, ex, metadata, ct) =>
            {
                _logger.LogError(ex,
                    "Permanently failed to process UserDeletedEvent: UserId={UserId}, TenantId={TenantId}",
                    message.UserId, message.TenantId);
                await Task.CompletedTask;
            },
            cancellationToken);
    }

    private Task SubscribeToUserDisabledEvents(CancellationToken cancellationToken)
    {
        return _userDisabledSubscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                try
                {
                    var metadataDoc = new BsonDocument
                    {
                        { "roles", new BsonArray(message.Roles) },
                        { "userCreatedAt", message.UserCreatedAt },
                        { "disabledAt", message.DisabledAt }
                    };
                    if (metadata != null)
                        foreach (var kvp in metadata)
                            metadataDoc[kvp.Key] = BsonValue.Create(kvp.Value);

                    await _auditService.RecordAuditLogAsync(
                        nameof(UserDisabledEvent),
                        message,
                        message.UserId,
                        message.TenantId,
                        message.UserName,
                        message.Email,
                        metadataDoc,
                        ct);

                    _logger.LogDebug("Recorded audit log for UserDisabledEvent: UserId={UserId}, TenantId={TenantId}",
                        message.UserId, message.TenantId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process UserDisabledEvent: UserId={UserId}, TenantId={TenantId}",
                        message.UserId, message.TenantId);
                    throw;
                }
            },
            3,
            async (message, ex, metadata, ct) =>
            {
                _logger.LogError(ex,
                    "Permanently failed to process UserDisabledEvent: UserId={UserId}, TenantId={TenantId}",
                    message.UserId, message.TenantId);
                await Task.CompletedTask;
            },
            cancellationToken);
    }

    private Task SubscribeToUserModifiedEvents(CancellationToken cancellationToken)
    {
        return _userModifiedSubscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                try
                {
                    var metadataDoc = new BsonDocument
                    {
                        { "changedProperties", new BsonArray(message.ChangedProperties) }
                    };
                    if (message.OldIsActive.HasValue) metadataDoc["oldIsActive"] = message.OldIsActive.Value;
                    if (message.OldEmail != null) metadataDoc["oldEmail"] = message.OldEmail;
                    if (message.OldFirstName != null) metadataDoc["oldFirstName"] = message.OldFirstName;
                    if (message.OldLastName != null) metadataDoc["oldLastName"] = message.OldLastName;
                    if (message.OldPhoneNumber != null) metadataDoc["oldPhoneNumber"] = message.OldPhoneNumber;
                    if (metadata != null)
                        foreach (var kvp in metadata)
                            metadataDoc[kvp.Key] = BsonValue.Create(kvp.Value);

                    await _auditService.RecordAuditLogAsync(
                        nameof(UserModifiedEvent),
                        message,
                        message.UserId,
                        message.TenantId,
                        message.UserName,
                        message.Email,
                        metadataDoc,
                        ct);

                    _logger.LogDebug(
                        "Recorded audit log for UserModifiedEvent: UserId={UserId}, TenantId={TenantId}, ChangedProperties={ChangedProperties}",
                        message.UserId, message.TenantId, string.Join(", ", message.ChangedProperties));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process UserModifiedEvent: UserId={UserId}, TenantId={TenantId}",
                        message.UserId, message.TenantId);
                    throw;
                }
            },
            3,
            async (message, ex, metadata, ct) =>
            {
                _logger.LogError(ex,
                    "Permanently failed to process UserModifiedEvent: UserId={UserId}, TenantId={TenantId}",
                    message.UserId, message.TenantId);
                await Task.CompletedTask;
            },
            cancellationToken);
    }

    /// <summary>
    ///     Unsubscribes from all queues when the service stops
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping user audit event subscribers...");

        try
        {
            await Task.WhenAll(
                _userCreatedSubscriber.UnsubscribeAsync(cancellationToken),
                _userDeletedSubscriber.UnsubscribeAsync(cancellationToken),
                _userDisabledSubscriber.UnsubscribeAsync(cancellationToken),
                _userModifiedSubscriber.UnsubscribeAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from queues");
        }

        await base.StopAsync(cancellationToken);
    }

    private static BsonDocument? CreateBsonDocumentFromDictionary(IDictionary<string, object>? dictionary)
    {
        if (dictionary == null || dictionary.Count == 0) return null;

        var doc = new BsonDocument();
        foreach (var kvp in dictionary) doc[kvp.Key] = BsonValue.Create(kvp.Value);
        return doc;
    }
}