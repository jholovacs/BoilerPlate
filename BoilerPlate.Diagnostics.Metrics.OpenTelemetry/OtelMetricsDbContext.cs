using BoilerPlate.Diagnostics.Database;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Diagnostics.Metrics.OpenTelemetry;

/// <summary>
///     OpenTelemetry-based implementation of <see cref="BaseMetricsDbContext" />.
///     Metrics are populated by <see cref="OtelCollectorMetricsHostedService" />, which scrapes
///     the OTEL collector's Prometheus exporter (e.g. http://otel-collector:8889/metrics).
/// </summary>
public sealed class OtelMetricsDbContext : BaseMetricsDbContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OtelMetricsDbContext" /> class.
    /// </summary>
    /// <param name="options">The options to be used by a DbContext.</param>
    public OtelMetricsDbContext(DbContextOptions<BaseMetricsDbContext> options)
        : base(options)
    {
    }
}
