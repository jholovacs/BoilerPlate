using BoilerPlate.Diagnostics.Database;
using BoilerPlate.Diagnostics.Database.Entities;
using BoilerPlate.Diagnostics.AuditLogs.MongoDb.ValueConverters;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace BoilerPlate.Diagnostics.AuditLogs.MongoDb;

/// <summary>
///     MongoDB implementation of <see cref="BaseAuditLogDbContext" /> for the audit_logs collection
///     (same store used by BoilerPlate.Services.Audit).
/// </summary>
public sealed class AuditLogsMongoDbContext : BaseAuditLogDbContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AuditLogsMongoDbContext" /> class.
    /// </summary>
    /// <param name="options">The options to be used by a DbContext (MongoDB).</param>
    public AuditLogsMongoDbContext(DbContextOptions<BaseAuditLogDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToCollection("audit_logs");

            // Match existing camelCase element names from BoilerPlate.Services.Audit
            entity.Property(e => e.EventType).HasElementName("eventType");
            entity.Property(e => e.UserId).HasElementName("userId");
            entity.Property(e => e.TenantId).HasElementName("tenantId");
            entity.Property(e => e.UserName).HasElementName("userName");
            entity.Property(e => e.Email).HasElementName("email");
            entity.Property(e => e.EventData).HasElementName("eventData")
                .HasConversion(BsonDocumentToStringConverter.Default);
            entity.Property(e => e.TraceId).HasElementName("traceId");
            entity.Property(e => e.ReferenceId).HasElementName("referenceId");
            entity.Property(e => e.EventTimestamp).HasElementName("eventTimestamp");
            entity.Property(e => e.CreatedAt).HasElementName("createdAt");
            entity.Property(e => e.Metadata).HasElementName("metadata")
                .HasConversion(BsonDocumentToStringConverter.Default);
        });
    }
}
