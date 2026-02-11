using BoilerPlate.Diagnostics.Database.Entities;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace BoilerPlate.Diagnostics.WebApi.Configuration;

/// <summary>
///     OData Entity Data Model for diagnostics: event logs, audit logs, and metrics.
/// </summary>
public static class ODataConfiguration
{
    /// <summary>
    ///     Builds the OData Entity Data Model.
    /// </summary>
    public static IEdmModel GetEdmModel()
    {
        var builder = new ODataConventionModelBuilder();

        var eventLogs = builder.EntitySet<EventLogEntry>("EventLogs");
        eventLogs.EntityType.HasKey(e => e.Id);

        var auditLogs = builder.EntitySet<AuditLogEntry>("AuditLogs");
        auditLogs.EntityType.HasKey(a => a.Id);

        var metrics = builder.EntitySet<MetricPoint>("Metrics");
        metrics.EntityType.HasKey(m => m.Id);

        return builder.GetEdmModel();
    }
}
