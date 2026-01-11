using BoilerPlate.ServiceBus.RabbitMq.Extensions;
using BoilerPlate.Services.Audit.Services;
using MongoDB.Driver;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

try
{
    Log.Information("Starting Audit Service");

    // Get MongoDB connection string from environment or configuration
    var mongoDbConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
                                  ?? builder.Configuration.GetConnectionString("MongoDb")
                                  ?? throw new InvalidOperationException(
                                      "MONGODB_CONNECTION_STRING environment variable or MongoDB connection string is required");

    // Get database name from connection string or use default
    var uri = new Uri(mongoDbConnectionString);
    var databaseName = uri.AbsolutePath.TrimStart('/');
    if (string.IsNullOrEmpty(databaseName)) databaseName = "logs"; // Default database name

    // Register MongoDB
    builder.Services.AddSingleton<IMongoClient>(sp => new MongoClient(mongoDbConnectionString));
    builder.Services.AddSingleton<IMongoDatabase>(sp =>
    {
        var client = sp.GetRequiredService<IMongoClient>();
        return client.GetDatabase(databaseName);
    });

    // Register AuditService
    builder.Services.AddScoped<AuditService>();

    // Add RabbitMQ service bus (connection string can be overridden via RABBITMQ_CONNECTION_STRING environment variable)
    builder.Services.AddRabbitMqServiceBus(builder.Configuration);

    // Register the event subscriber as a hosted service
    builder.Services.AddHostedService<UserAuditEventSubscriber>();

    var host = builder.Build();

    Log.Information("Audit Service started successfully");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Audit Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}