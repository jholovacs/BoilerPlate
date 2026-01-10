using BoilerPlate.Authentication.Database.PostgreSql.Extensions;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Extensions;
using BoilerPlate.Authentication.WebApi.Filters;
using BoilerPlate.Authentication.WebApi.Services;
using BoilerPlate.ServiceBus.RabbitMq.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.Edm;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MongoDB;
using MongoDB.Driver;

// Configure Serilog before creating the builder
// Check if we're running in design-time mode (EF Core migrations)
// EF Core tools set certain environment variables or use specific patterns
// Check multiple indicators to detect design-time operations
var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName.ToLowerInvariant();
var commandLineArgs = Environment.GetCommandLineArgs();
var efDesignOperation = Environment.GetEnvironmentVariable("EF_DESIGN_OPERATION");
var efToolsPath = Environment.GetEnvironmentVariable("EF_TOOLS_PATH");

var isDesignTime = 
    !string.IsNullOrWhiteSpace(efDesignOperation) ||
    !string.IsNullOrWhiteSpace(efToolsPath) ||
    processName.Contains("ef") || 
    processName.Contains("dotnet-ef") ||
    commandLineArgs.Any(arg => 
        (arg.Contains("ef", StringComparison.OrdinalIgnoreCase) && !arg.Contains("efcore", StringComparison.OrdinalIgnoreCase)) ||
        arg.Contains("design", StringComparison.OrdinalIgnoreCase) ||
        arg.Contains("migration", StringComparison.OrdinalIgnoreCase) ||
        arg.Contains("database", StringComparison.OrdinalIgnoreCase));

// For design-time operations, we'll skip MongoDB initialization and use console logging
// This prevents errors during 'dotnet ef' commands
if (isDesignTime)
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Warning() // Reduce verbosity during design-time
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
        .WriteTo.Console()
        .CreateLogger();
}
else
{
    // Normal runtime mode - configure MongoDB logging
    var mongoDbConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");
    var loggerConfig = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "BoilerPlate.Authentication.WebApi");

    // Configure MongoDB if connection string is provided
    if (!string.IsNullOrWhiteSpace(mongoDbConnectionString))
    {
        try
        {
            // Parse database name from connection string
            var uri = new Uri(mongoDbConnectionString);
            var databaseName = uri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = "logs";
            }

            var mongoClient = new MongoClient(mongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(databaseName);

            loggerConfig.WriteTo.MongoDB(
                mongoDatabase,
                collectionName: "logs",
                restrictedToMinimumLevel: LogEventLevel.Information);
        }
        catch (Exception ex)
        {
            // If MongoDB configuration fails, log to console as fallback
            loggerConfig.WriteTo.Console();
            Console.WriteLine($"Warning: Failed to configure MongoDB logging: {ex.Message}");
        }
    }
    else
    {
        // No MongoDB connection string - use console logging as fallback
        loggerConfig.WriteTo.Console();
        // Throw error for runtime mode if MongoDB connection string is missing
        throw new InvalidOperationException(
            "MONGODB_CONNECTION_STRING environment variable is required. " +
            "Format: mongodb://username:password@host:port/database or mongodb://host:port/database");
    }

    Log.Logger = loggerConfig.CreateLogger();
}

try
{
    Log.Information("Starting web host");

    var builder = WebApplication.CreateBuilder(args);
    
    // Use Serilog instead of default logging
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers()
        .AddOData(options => options
            .Select()
            .Filter()
            .OrderBy()
            .Expand()
            .Count()
            .SetMaxTop(100)
            .AddRouteComponents("odata", ODataConfiguration.GetEdmModel()));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Authentication API",
            Version = "v1",
            Description = "OAuth2 authentication API with JWT tokens using RS256 (RSA asymmetric encryption). " +
                          "Includes RESTful endpoints and OData query endpoints for all authentication entities."
        });

        // Add JWT authentication to Swagger
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\". " +
                          "Obtain a token from the /oauth/token endpoint.",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT"
        });

        // Enable XML comments for better documentation
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }

        // Add operation filter to include authorization requirements in Swagger
        c.OperationFilter<AuthorizationOperationFilter>();
        
        // Add schema filter to include realistic examples in Swagger
        c.SchemaFilter<ExampleSchemaFilter>();
        
        // Include OData endpoints
        c.DocumentFilter<ODataDocumentFilter>();
    });

    // During design-time (EF Core migrations), only register essential services
    // Skip services that require external connections (RabbitMQ, MongoDB, etc.)
    if (!isDesignTime)
    {
        // Add RabbitMQ service bus (connection string can be overridden via RABBITMQ_CONNECTION_STRING environment variable)
        builder.Services.AddRabbitMqServiceBus(builder.Configuration);
    }

    // Add PostgreSQL database (required for migrations)
    builder.Services.AddAuthenticationDatabasePostgreSql(builder.Configuration);

    // Add authentication services (this registers IAuthenticationService, IUserService, etc.)
    builder.Services.AddAuthenticationServices(builder.Configuration);

    // Add authorization policies (must be added after authentication)
    builder.Services.AddAuthorizationPolicies();

    // Only register JWT authentication and hosted services during runtime, not during design-time
    // During design-time (EF Core migrations), these services aren't needed
    if (!isDesignTime)
    {
        // Add JWT authentication with RS256
        builder.Services.AddJwtAuthentication(builder.Configuration);

        // Add admin user initialization service
        builder.Services.AddHostedService<AdminUserInitializationService>();

        // Add MongoDB log index service to create timestamp index on startup
        builder.Services.AddHostedService<MongoDbLogIndexService>();
    }

    var app = builder.Build();

    // During design-time, don't configure the HTTP pipeline - just return early
    // EF Core tools only need the DbContext, not the full running application
    if (isDesignTime)
    {
        // Don't configure or run the app during design-time
        // Just building it is enough for EF Core tools to get the DbContext
        return;
    }

    // Configure the HTTP request pipeline
    // Enable Swagger in all environments (can be restricted to Development if needed)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Authentication API v1");
        c.RoutePrefix = "swagger"; // Swagger UI available at /swagger
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
        c.EnableValidator();
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        c.DefaultModelsExpandDepth(-1); // Collapse models by default
        c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
    });

    app.UseHttpsRedirection();

    // Authentication must come before Authorization
    // These are only needed during runtime, not during design-time
    if (!isDesignTime)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    // Map controllers (includes OData controllers)
    app.MapControllers();

    // Only run the app during runtime, not during design-time
    if (!isDesignTime)
    {
        app.Run();
    }
}
catch (Microsoft.Extensions.Hosting.HostAbortedException)
{
    // HostAbortedException is expected during EF Core design-time operations
    // EF Core tools intentionally abort the host after getting the DbContext
    if (isDesignTime)
    {
        // During design-time, this is expected and not an error
        Log.Debug("Host aborted during design-time operation (this is expected)");
    }
    else
    {
        // During runtime, this is unexpected
        Log.Fatal("Host aborted unexpectedly during runtime");
        throw;
    }
}
catch (Exception ex)
{
    // Only log as fatal if not during design-time
    if (isDesignTime)
    {
        Log.Warning(ex, "Exception during design-time operation (may be expected): {Message}", ex.Message);
    }
    else
    {
        Log.Fatal(ex, "Host terminated unexpectedly");
        throw;
    }
}
finally
{
    Log.CloseAndFlush();
}
