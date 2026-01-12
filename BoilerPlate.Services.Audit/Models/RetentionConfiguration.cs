namespace BoilerPlate.Services.Audit.Models;

/// <summary>
///     Configuration for log and audit record retention periods
/// </summary>
public class RetentionConfiguration
{
    /// <summary>
    ///     Default retention period for audit records (7 years)
    /// </summary>
    public TimeSpan AuditRecordsRetention { get; set; } = TimeSpan.FromDays(365 * 7); // 7 years

    /// <summary>
    ///     Default retention period for trace logs (48 hours)
    /// </summary>
    public TimeSpan TraceLogsRetention { get; set; } = TimeSpan.FromHours(48);

    /// <summary>
    ///     Default retention period for debug logs (72 hours)
    /// </summary>
    public TimeSpan DebugLogsRetention { get; set; } = TimeSpan.FromHours(72);

    /// <summary>
    ///     Default retention period for informational logs (30 days)
    /// </summary>
    public TimeSpan InformationLogsRetention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    ///     Default retention period for warning logs (90 days)
    /// </summary>
    public TimeSpan WarningLogsRetention { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    ///     Default retention period for error logs (90 days)
    /// </summary>
    public TimeSpan ErrorLogsRetention { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    ///     Default retention period for critical/fatal logs (90 days)
    /// </summary>
    public TimeSpan CriticalLogsRetention { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    ///     Frequency at which retention cleanup runs (default: 24 hours)
    /// </summary>
    public TimeSpan CleanupFrequency { get; set; } = TimeSpan.FromHours(24);
}
