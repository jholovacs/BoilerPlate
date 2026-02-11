using BoilerPlate.Diagnostics.AuditLogs.MongoDb.Extensions;
using BoilerPlate.Diagnostics.EventLogs.MongoDb.Extensions;
using BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Extensions;
using BoilerPlate.Diagnostics.WebApi.Configuration;
using BoilerPlate.Diagnostics.WebApi.Extensions;
using Microsoft.AspNetCore.OData;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Wire diagnostics data stores: audit logs and event logs (MongoDB), metrics (OTEL)
builder.Services.AddDiagnosticsAuditLogsMongoDb(builder.Configuration);
builder.Services.AddDiagnosticsEventLogsMongoDb(builder.Configuration);
builder.Services.AddDiagnosticsMetricsOpenTelemetry(builder.Configuration);

// JWT validation (same tokens as Authentication WebApi) and authorization (Service Administrator = all tenants; others = tenant-scoped)
builder.Services.AddDiagnosticsJwtAuthentication(builder.Configuration);

builder.Services.AddControllers()
    .AddOData(options => options
        .Select()
        .Filter()
        .OrderBy()
        .Expand()
        .Count()
        .SetMaxTop(500)
        .AddRouteComponents("odata", ODataConfiguration.GetEdmModel()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Diagnostics API",
        Version = "v1",
        Description = "Read-only OData API for browsing event logs (MongoDB), audit logs (MongoDB), and OpenTelemetry metrics (in-memory by default)."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Diagnostics API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
