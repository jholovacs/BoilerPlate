using BoilerPlate.Diagnostics.Database;
using BoilerPlate.Diagnostics.Database.Entities;
using BoilerPlate.Diagnostics.EventLogs.MongoDb.ValueConverters;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace BoilerPlate.Diagnostics.EventLogs.MongoDb;

/// <summary>
///     MongoDB implementation of <see cref="BaseEventLogDbContext" /> for the logs collection
///     (e.g. Serilog.Sinks.MongoDB with collection name "logs").
/// </summary>
public sealed class EventLogsMongoDbContext : BaseEventLogDbContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="EventLogsMongoDbContext" /> class.
    /// </summary>
    /// <param name="options">The options to be used by a DbContext (MongoDB).</param>
    public EventLogsMongoDbContext(DbContextOptions<EventLogsMongoDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<EventLogEntry>(entity =>
        {
            entity.ToCollection("logs");

            entity.Property(e => e.Id).HasConversion(ObjectIdToLongConverter.Default);

            // Serilog.Sinks.MongoDB uses PascalCase for main fields
            entity.Property(e => e.Timestamp).HasElementName("Timestamp");
            entity.Property(e => e.Level).HasElementName("Level");
            entity.Property(e => e.Source).HasElementName("Source");
            entity.Property(e => e.MessageTemplate).HasElementName("MessageTemplate");
            entity.Property(e => e.Message).HasElementName("Message");
            entity.Property(e => e.TraceId).HasElementName("TraceId");
            entity.Property(e => e.SpanId).HasElementName("SpanId");
            entity.Property(e => e.Exception).HasElementName("Exception");
            // Properties is BsonDocument in MongoDB; EF provider does not support it. Ignore to avoid mapping errors.
            // Tenant filtering for non-Service-Admin is handled in controller (returns empty when Properties unavailable).
            entity.Ignore(e => e.Properties);
        });
    }
}
