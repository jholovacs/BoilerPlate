using BoilerPlate.Diagnostics.Database.Entities;

namespace BoilerPlate.Diagnostics.AuditLogs.MongoDb.Services;

/// <summary>
///     Raw MongoDB queries for audit logs. Bypasses EF to avoid OData/EnableQuery translation issues.
/// </summary>
public interface IAuditLogsRawQueryService
{
    /// <summary>
    ///     Queries audit logs with optional tenant filter and pagination.
    /// </summary>
    /// <param name="tenantId">When set, filters by tenantId. Null = no tenant filter (Service Admin).</param>
    /// <param name="orderByDesc">True = EventTimestamp desc, false = EventTimestamp asc.</param>
    /// <param name="top">Max results.</param>
    /// <param name="skip">Number to skip.</param>
    /// <param name="includeCount">Whether to return total count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<(List<AuditLogEntry> Results, long? Count)> QueryAsync(
        Guid? tenantId,
        bool orderByDesc,
        int top,
        int skip,
        bool includeCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a single audit log by key. When tenantId is set, verifies the log belongs to that tenant.
    /// </summary>
    Task<AuditLogEntry?> GetByIdAsync(string key, Guid? tenantId, CancellationToken cancellationToken = default);
}
