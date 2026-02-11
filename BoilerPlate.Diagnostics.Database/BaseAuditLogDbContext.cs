using BoilerPlate.Diagnostics.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Diagnostics.Database;

/// <summary>
///     Abstract database context for the audit logs store (e.g. user/tenant audit events in MongoDB).
///     Concrete implementations provide the actual connection to the audit logs data store.
/// </summary>
public abstract class BaseAuditLogDbContext : DbContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BaseAuditLogDbContext" /> class.
    /// </summary>
    /// <param name="options">The options to be used by a DbContext.</param>
    protected BaseAuditLogDbContext(DbContextOptions<BaseAuditLogDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    ///     Audit log entries.
    /// </summary>
    public DbSet<AuditLogEntry> AuditLogs { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.UserName).HasMaxLength(256);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EventData).IsRequired();
            entity.Property(e => e.TraceId).HasMaxLength(64);
            entity.Property(e => e.ReferenceId).HasMaxLength(128);
            entity.Property(e => e.EventTimestamp).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Metadata);

            entity.HasIndex(e => e.EventTimestamp);
            entity.HasIndex(e => new { e.TenantId, e.EventTimestamp });
            entity.HasIndex(e => new { e.UserId, e.EventTimestamp });
            entity.HasIndex(e => e.EventType);
        });
    }
}
