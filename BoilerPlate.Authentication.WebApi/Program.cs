using System.Diagnostics;
using System.IO;
using System.Reflection;
using BoilerPlate.Authentication.Database.PostgreSql.Extensions;
using BoilerPlate.Authentication.WebApi.Utilities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Extensions;
using BoilerPlate.Authentication.WebApi.Filters;
using BoilerPlate.Authentication.WebApi.Middleware;
using BoilerPlate.Authentication.WebApi.Services;
using BoilerPlate.Observability.Abstractions;
using BoilerPlate.Observability.OpenTelemetry.Extensions;
using BoilerPlate.ServiceBus.RabbitMq.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.OData;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Sinks.RabbitMQ;
using Swashbuckle.AspNetCore.SwaggerUI;

// Generate ML-DSA keys if requested (e.g. make setup-keys)
var genKeysIdx = Array.IndexOf(args, "--generate-mldsa-keys");
if (genKeysIdx >= 0)
{
    var outputDir = genKeysIdx + 1 < args.Length ? args[genKeysIdx + 1] : null;
    var path = MlDsaKeyGenerator.GenerateAndWriteToDirectory(outputDir);
    Console.WriteLine($"Generated ML-DSA keys: {path}");
    Environment.Exit(0);
}

// Configure Serilog before creating the builder
// Check if we're running in design-time mode (EF Core migrations)
// EF Core tools set certain environment variables or use specific patterns
// Check multiple indicators to detect design-time operations
var processName = Process.GetCurrentProcess().ProcessName.ToLowerInvariant();
var commandLineArgs = Environment.GetCommandLineArgs();
var efDesignOperation = Environment.GetEnvironmentVariable("EF_DESIGN_OPERATION");
var efToolsPath = Environment.GetEnvironmentVariable("EF_TOOLS_PATH");

var isDesignTime =
    !string.IsNullOrWhiteSpace(efDesignOperation) ||
    !string.IsNullOrWhiteSpace(efToolsPath) ||
    processName.Contains("ef") ||
    processName.Contains("dotnet-ef") ||
    commandLineArgs.Any(arg =>
        (arg.Contains("ef", StringComparison.OrdinalIgnoreCase) &&
         !arg.Contains("efcore", StringComparison.OrdinalIgnoreCase)) ||
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
    // Normal runtime mode - configure RabbitMQ logging (trace level and above)
    // Event logs flow: Serilog -> RabbitMQ queue -> EventLogs service -> MongoDB + topic for real-time
    var rabbitConnectionString = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");
    var loggerConfig = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "BoilerPlate.Authentication.WebApi");

    loggerConfig.WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information);

    if (!string.IsNullOrWhiteSpace(rabbitConnectionString))
    {
        try
        {
            var uri = new Uri(rabbitConnectionString);
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5672;
            var user = uri.UserInfo?.Split(':')[0] ?? "guest";
            var pass = uri.UserInfo?.Contains(':') == true ? uri.UserInfo.Split(':', 2)[1] : "guest";

            loggerConfig.WriteTo.RabbitMQ((clientConfig, sinkConfig) =>
            {
                clientConfig.Username = user;
                clientConfig.Password = pass;
                clientConfig.Hostnames.Add(host);
                clientConfig.Port = port;
                clientConfig.Exchange = "event-logs";
                clientConfig.ExchangeType = "fanout";
                clientConfig.AutoCreateExchange = true;
                clientConfig.DeliveryMode = RabbitMQDeliveryMode.Durable;
                sinkConfig.RestrictedToMinimumLevel = LogEventLevel.Verbose;
                // Use ExpressionTemplate to send template (@mt), rendered message (@m), properties (..@p),
                // and full logging context to MongoDB for diagnostics.
                sinkConfig.TextFormatter = new ExpressionTemplate("{ {@t, @mt, @m, @l, @x, ..@p} }\n");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to configure RabbitMQ logging: {ex.Message}");
        }
    }
    else
    {
        throw new InvalidOperationException(
            "RABBITMQ_CONNECTION_STRING environment variable is required for event log streaming. " +
            "Format: amqp://username:password@host:port/");
    }

    Log.Logger = loggerConfig.CreateLogger();
}

try
{
    Log.Information("Starting web host");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog instead of default logging
    builder.Host.UseSerilog();

    // Add HttpContextAccessor for accessing HttpContext in services/middleware
    builder.Services.AddHttpContextAccessor();

    // Configure Data Protection for persistent key storage
    // In containerized environments, mount a volume to persist keys across container restarts
    // Set DATA_PROTECTION_KEYS_PATH environment variable to specify a persistent volume path
    var dataProtectionKeysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");
    var dataProtectionConfigured = false;

    if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
    {
        try
        {
            // Ensure the directory exists
            Directory.CreateDirectory(dataProtectionKeysPath);
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
            Log.Information("Data Protection keys will be persisted to: {Path}", dataProtectionKeysPath);
            dataProtectionConfigured = true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to configure Data Protection with custom path {Path}, trying default location", dataProtectionKeysPath);
        }
    }

    // If custom path wasn't set or failed, try container-friendly path
    if (!dataProtectionConfigured)
    {
        var persistentPath = "/app/data-protection-keys";
        try
        {
            if (Directory.Exists("/app"))
            {
                Directory.CreateDirectory(persistentPath);
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(persistentPath));
                Log.Information("Data Protection keys will be persisted to: {Path}", persistentPath);
                dataProtectionConfigured = true;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to configure Data Protection with container path {Path}, trying Windows path", persistentPath);
        }
    }

    // If container path failed, try Windows ApplicationData path
    if (!dataProtectionConfigured)
    {
        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "BoilerPlate", "DataProtection-Keys");
            Directory.CreateDirectory(appDataPath);
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(appDataPath));
            Log.Information("Data Protection keys will be persisted to: {Path}", appDataPath);
            dataProtectionConfigured = true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to configure Data Protection with persistent path, keys may not persist across restarts: {Message}", ex.Message);
            // Data Protection will use default location (ephemeral) - this is acceptable for development
        }
    }

    // Register OpenTelemetry metrics recorder
    // During design-time, skip metrics registration to avoid issues
    if (!isDesignTime)
    {
        // Add OpenTelemetry metrics with OTLP exporter
        builder.Services.AddOpenTelemetryMetrics(builder.Configuration, "BoilerPlate.Authentication");
    }
    else
    {
        // During design-time, register null implementation to avoid issues
        builder.Services.AddSingleton<IMetricsRecorder, NullMetricsRecorder>();
    }

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
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Authentication API",
            Version = "v1",
            Description = "OAuth2 authentication API with JWT tokens using RS256 (RSA asymmetric encryption). " +
                          "Includes RESTful endpoints and OData query endpoints for all authentication entities."
        });

        // Add JWT authentication to Swagger
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description =
                "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\". " +
                "Obtain a token from the /oauth/token endpoint.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT"
        });

        // Enable XML comments for better documentation
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);

        // Add operation filter to include authorization requirements in Swagger
        c.OperationFilter<AuthorizationOperationFilter>();

        // Add operation filter for OAuth endpoint documentation
        c.OperationFilter<OAuthOperationFilter>();

        // Add schema filter to include realistic examples in Swagger
        c.SchemaFilter<ExampleSchemaFilter>();

        // Include OData endpoints
        c.DocumentFilter<ODataDocumentFilter>();
    });

    // During design-time (EF Core migrations), only register essential services
    // Skip services that require external connections (RabbitMQ, MongoDB, etc.)
    if (!isDesignTime)
        // Add RabbitMQ service bus (connection string can be overridden via RABBITMQ_CONNECTION_STRING environment variable)
        builder.Services.AddRabbitMqServiceBus(builder.Configuration);

    // RabbitMQ Management proxy (Service Administrators get auto-login via Basic Auth)
    var rabbitConn = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");
    var rabbitMqProxyOptions = new BoilerPlate.Authentication.WebApi.Configuration.RabbitMqProxyOptions();
    if (!string.IsNullOrWhiteSpace(rabbitConn) && Uri.TryCreate(rabbitConn, UriKind.Absolute, out var rabbitUri))
    {
        var host = rabbitUri.Host;
        var port = 15672; // Management UI port (AMQP is 5672)
        var user = rabbitUri.UserInfo?.Split(':')[0] ?? "guest";
        var pass = rabbitUri.UserInfo?.Contains(':') == true ? rabbitUri.UserInfo.Split(':', 2)[1] : "guest";
        rabbitMqProxyOptions.ManagementBaseUrl = $"http://{host}:{port}";
        rabbitMqProxyOptions.Username = user;
        rabbitMqProxyOptions.Password = pass;
    }
    builder.Services.AddSingleton(rabbitMqProxyOptions);
    builder.Services.AddHttpClient("RabbitMqProxy")
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

    // Add CORS - allowed origins from CORS_ALLOWED_ORIGINS (comma-separated), or allow any for local dev if unset
    var corsOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (string.IsNullOrWhiteSpace(corsOrigins))
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            }
            else
            {
                foreach (var origin in corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    policy.WithOrigins(origin);
                policy.AllowAnyMethod()
                    .AllowAnyHeader();
            }
        });
    });

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

        // Event logs are written by BoilerPlate.Services.EventLogs (consumes from RabbitMQ, writes to MongoDB)
    }

    var app = builder.Build();

    // During design-time, don't configure the HTTP pipeline - just return early
    // EF Core tools only need the DbContext, not the full running application
    if (isDesignTime)
        // Don't configure or run the app during design-time
        // Just building it is enough for EF Core tools to get the DbContext
        return;

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
        c.DocExpansion(DocExpansion.List);
        c.DefaultModelsExpandDepth(-1); // Collapse models by default
        c.DefaultModelRendering(ModelRendering.Model);
    });

    // Skip UseHttpsRedirection: API runs behind nginx (HTTPS) in Docker; no HTTPS port configured.
    // CORS must be before UseRouting and endpoints
    app.UseCors();

    // Enable routing (required for route-based middleware to work correctly)
    app.UseRouting();

    // Global API exception handler - ensures unhandled exceptions return JSON 500 instead of empty/broken response
    app.UseMiddleware<BoilerPlate.Authentication.WebApi.Middleware.ApiExceptionHandlerMiddleware>();

    // Request metrics middleware - must come after routing to access route information
    // This tracks request duration and count with route tags
    // During design-time, skip this middleware
    if (!isDesignTime)
    {
        app.UseMiddleware<RequestMetricsMiddleware>();
    }

    // Authentication must come before Authorization
    // These are only needed during runtime, not during design-time
    if (!isDesignTime)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        // Enrich log context with username from authenticated user
        // This must come after UseAuthentication/UseAuthorization so the user is authenticated
        app.UseMiddleware<UsernameLogEnricherMiddleware>();
    }

    // Map controllers (includes OData controllers)
    app.MapControllers();

    // Only run the app during runtime, not during design-time
    if (!isDesignTime) app.Run();
}
catch (HostAbortedException)
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