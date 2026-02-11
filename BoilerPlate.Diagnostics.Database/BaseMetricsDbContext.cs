using BoilerPlate.Diagnostics.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Diagnostics.Database;

/// <summary>
///     Abstract database context for OpenTelemetry metrics (OTEL collector backend).
///     Concrete implementations provide the actual connection or adapter to the metrics data source.
/// </summary>
public abstract class BaseMetricsDbContext : DbContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BaseMetricsDbContext" /> class.
    /// </summary>
    /// <param name="options">The options to be used by a DbContext.</param>
    protected BaseMetricsDbContext(DbContextOptions<BaseMetricsDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    ///     Metric data points from OpenTelemetry.
    /// </summary>
    public DbSet<MetricPoint> Metrics { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<MetricPoint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.MetricName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(64);
            entity.Property(e => e.InstrumentType).HasMaxLength(64);
            entity.Property(e => e.Attributes);
            entity.Property(e => e.Source).HasMaxLength(256);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.MetricName, e.Timestamp });
        });
    }
}
