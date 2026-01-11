using BoilerPlate.Observability.Abstractions;
using BoilerPlate.Observability.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace BoilerPlate.Observability.OpenTelemetry.Extensions;

/// <summary>
///     Extension methods for registering OpenTelemetry metrics
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds OpenTelemetry metrics collection and registers <see cref="IMetricsRecorder" /> with OpenTelemetry implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="serviceName">The service name for the meter (optional, defaults to "BoilerPlate").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenTelemetryMetrics(
        this IServiceCollection services,
        IConfiguration configuration,
        string? serviceName = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        // Get OTEL Collector endpoint from configuration
        // Support both environment variable and configuration key
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                           ?? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                           ?? configuration["OpenTelemetry:Exporter:OtlpEndpoint"];

        // Default to OTEL Collector service in Docker Compose
        if (string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            otlpEndpoint = "http://otel-collector:4317";
        }

        var serviceNameValue = serviceName ?? "BoilerPlate.Authentication";

        // Add OpenTelemetry metrics
        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(
                        ResourceBuilder.CreateDefault()
                            .AddService(serviceNameValue)
                            .AddAttributes(new Dictionary<string, object>
                            {
                                ["service.name"] = serviceNameValue,
                                ["service.version"] = "1.0.0"
                            }))
                    .AddMeter(serviceNameValue); // Add the meter for our custom metrics
                // Note: Runtime and Process instrumentation require additional packages:
                // - OpenTelemetry.Instrumentation.Runtime
                // - OpenTelemetry.Instrumentation.Process
                // These can be added later if needed

                // Add OTLP exporter to send metrics to OTEL Collector
                // OTLP exporter uses gRPC by default (port 4317)
                metrics.AddOtlpExporter(options =>
                {
                    // Parse endpoint URL - OTLP uses gRPC endpoint (typically http://host:4317)
                    Uri? endpointUri = null;

                    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    {
                        // If already a full URL, use it
                        if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var parsedUri))
                        {
                            endpointUri = parsedUri;
                        }
                        else
                        {
                            // If not a full URL, assume it's just the hostname:port
                            var host = otlpEndpoint.TrimEnd('/');
                            if (!host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                                !host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            {
                                host = "http://" + host;
                            }

                            if (Uri.TryCreate(host, UriKind.Absolute, out var hostUri))
                            {
                                endpointUri = hostUri;
                            }
                        }
                    }

                    // Set endpoint (defaults to http://localhost:4317 if not specified)
                    options.Endpoint = endpointUri ?? new Uri("http://otel-collector:4317");
                });
            });

        // Register the OpenTelemetry-based metrics recorder
        services.AddSingleton<IMetricsRecorder>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<OpenTelemetryMetricsRecorder>>();
            return new OpenTelemetryMetricsRecorder(logger, serviceNameValue);
        });

        return services;
    }
}
