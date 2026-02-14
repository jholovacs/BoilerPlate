using BoilerPlate.ServiceBus.RabbitMq.Extensions;
using BoilerPlate.Services.EventLogs.Services;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

try
{
    Log.Information("Starting Event Logs Service");

    builder.Services.AddRabbitMqServiceBus(builder.Configuration);
    builder.Services.AddHostedService<EventLogConsumerService>();

    var host = builder.Build();

    Log.Information("Event Logs Service started successfully");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Event Logs Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
