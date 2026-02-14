using BoilerPlate.Diagnostics.Database.Entities;

namespace BoilerPlate.Diagnostics.EventLogs.MongoDb.Services;

/// <summary>
///     Raw MongoDB queries for event logs when tenant filtering is required.
///     Used for non-Service-Admin users since EF Core cannot map Properties (BsonDocument).
/// </summary>
public interface IEventLogsRawQueryService
{
    /// <summary>
    ///     Queries event logs with optional tenant filter, OData-style filters, and pagination.
    /// </summary>
    /// <param name="tenantId">When set, filters by Properties.tenantId. Null = no tenant filter (Service Admin).</param>
    /// <param name="levelFilter">Optional Level eq filter.</param>
    /// <param name="messageContains">Optional Message contains filter.</param>
    /// <param name="orderByDesc">True = Timestamp desc, false = Timestamp asc.</param>
    /// <param name="top">Max results.</param>
    /// <param name="skip">Number to skip.</param>
    /// <param name="includeCount">Whether to return total count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results and optional count.</returns>
    Task<(List<EventLogEntry> Results, long? Count)> QueryAsync(
        Guid? tenantId,
        string? levelFilter,
        string? messageContains,
        bool orderByDesc,
        int top,
        int skip,
        bool includeCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a single event log by key. When tenantId is set, verifies the log belongs to that tenant.
    /// </summary>
    Task<EventLogEntry?> GetByIdAsync(long key, Guid? tenantId, CancellationToken cancellationToken = default);
}
