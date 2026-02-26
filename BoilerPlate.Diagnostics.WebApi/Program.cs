using BoilerPlate.Diagnostics.AuditLogs.MongoDb.Extensions;
using BoilerPlate.Diagnostics.EventLogs.MongoDb.Extensions;
using BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Extensions;
using BoilerPlate.Diagnostics.WebApi.Configuration;
using BoilerPlate.Diagnostics.WebApi.Extensions;
using BoilerPlate.Diagnostics.WebApi.Hubs;
using BoilerPlate.Diagnostics.WebApi.Services;
using BoilerPlate.ServiceBus.RabbitMq.Extensions;
using Microsoft.AspNetCore.OData;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Wire diagnostics data stores: audit logs and event logs (MongoDB), metrics (OTEL)
builder.Services.AddDiagnosticsAuditLogsMongoDb(builder.Configuration);
builder.Services.AddDiagnosticsEventLogsMongoDb(builder.Configuration);
builder.Services.AddDiagnosticsMetricsOpenTelemetry(builder.Configuration);

// JWT validation (same tokens as Authentication WebApi) and authorization (Service Administrator = all tenants; others = tenant-scoped)
builder.Services.AddDiagnosticsJwtAuthentication(builder.Configuration);

// RabbitMQ for subscribing to EventLogPublishedEvent (real-time SignalR)
builder.Services.AddRabbitMqServiceBus(builder.Configuration);
builder.Services.AddHostedService<EventLogRealtimeService>();

// RabbitMQ Management API (queue/exchange monitoring, purge) - Service Administrators only
builder.Services.Configure<RabbitMqManagementOptions>(options =>
{
    builder.Configuration.GetSection(RabbitMqManagementOptions.SectionName).Bind(options);
    var connStr = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING")
        ?? builder.Configuration["RabbitMq:ConnectionString"]
        ?? "";
    if (!string.IsNullOrWhiteSpace(connStr) && Uri.TryCreate(connStr, UriKind.Absolute, out var uri))
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            options.BaseUrl = $"http://{uri.Host}:15672";
        if (string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            options.Username = parts[0];
            options.Password = parts.Length > 1 ? parts[1] : "";
        }
    }
});
builder.Services.AddHttpClient<RabbitMqManagementService>();

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials();
    });
});

builder.Services.AddControllers()
    .AddOData(options => options
        .Select()
        .Filter()
        .OrderBy()
        .Expand()
        .Count()
        .SetMaxTop(2500)
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

// Global exception handler: always return JSON error body so client can see the actual error
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var msg = ex?.Message ?? "Unknown error";
        var type = ex?.GetType().Name ?? "Unknown";
        var inner = ex?.InnerException?.Message;
        var stack = app.Environment.IsDevelopment() ? ex?.StackTrace : null;
        var json = System.Text.Json.JsonSerializer.Serialize(new { error = msg, type, inner, stack });
        await context.Response.WriteAsync(json);
    });
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Diagnostics API v1");
    c.RoutePrefix = "swagger";
});

// Skip UseHttpsRedirection: APIs run behind nginx (HTTPS) in Docker; no HTTPS port configured.
app.UseCors();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<EventLogsHub>("/hubs/event-logs");

app.Run();
