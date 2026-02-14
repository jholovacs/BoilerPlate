using BoilerPlate.Diagnostics.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Diagnostics.Database;

/// <summary>
///     Abstract database context for the event logs store (e.g. application logs in MongoDB).
///     Concrete implementations provide the actual connection to the logs data store.
/// </summary>
public abstract class BaseEventLogDbContext : DbContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BaseEventLogDbContext" /> class.
    /// </summary>
    /// <param name="options">The options to be used by a DbContext.</param>
    protected BaseEventLogDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    ///     Event log entries.
    /// </summary>
    public DbSet<EventLogEntry> EventLogs { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<EventLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Level).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Source).HasMaxLength(256);
            entity.Property(e => e.Message).IsRequired();
            entity.Property(e => e.TraceId).HasMaxLength(64);
            entity.Property(e => e.SpanId).HasMaxLength(32);
            entity.Property(e => e.Exception);
            entity.Property(e => e.Properties);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.Timestamp, e.Level });
            entity.HasIndex(e => e.TraceId);
        });
    }
}
