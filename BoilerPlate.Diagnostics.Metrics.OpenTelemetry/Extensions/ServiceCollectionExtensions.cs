using BoilerPlate.Diagnostics.Database;
using BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Configuration;
using BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Extensions;

/// <summary>
///     DI extensions for registering OpenTelemetry metrics context with the diagnostics API.
///     When <see cref="OtelCollectorMetricsOptions.PrometheusMetricsUrl" /> is set (or derived from OTEL_EXPORTER_OTLP_ENDPOINT),
///     a background service scrapes the OTEL collector's Prometheus exporter and populates the in-memory metrics store.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds <see cref="BaseMetricsDbContext" /> implemented for OpenTelemetry metrics, backed by the OTEL collector.
    ///     Binds <see cref="OtelCollectorMetricsOptions" /> from "DiagnosticsMetrics" and configures scraping from the
    ///     collector's Prometheus endpoint (default port 8889). If no URL is set, metrics remain empty.
    /// </summary>
    public static IServiceCollection AddDiagnosticsMetricsOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OtelCollectorMetricsOptions>(options =>
        {
            configuration.GetSection(OtelCollectorMetricsOptions.SectionName).Bind(options);
            // Resolve Prometheus URL if not set: derive from OTEL_EXPORTER_OTLP_ENDPOINT (e.g. http://otel-collector:4317 -> http://otel-collector:8889/metrics)
            if (string.IsNullOrWhiteSpace(options.PrometheusMetricsUrl))
            {
                var otlp = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                           ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    try
                    {
                        var uri = new Uri(otlp.Trim());
                        var builder = new UriBuilder(uri) { Port = 8889, Path = "metrics" };
                        options.PrometheusMetricsUrl = builder.Uri.ToString();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        });

        var databaseName = configuration["DiagnosticsMetrics:DatabaseName"] ?? "DiagnosticsMetrics";
        services.AddDiagnosticsMetricsOpenTelemetry(databaseName);

        services.AddHttpClient<OtelCollectorMetricsScraperService>();
        services.AddHostedService<OtelCollectorMetricsHostedService>();

        return services;
    }

    /// <summary>
    ///     Adds <see cref="BaseMetricsDbContext" /> with in-memory store. Use the configuration overload to enable scraping from the OTEL collector.
    /// </summary>
    public static IServiceCollection AddDiagnosticsMetricsOpenTelemetry(
        this IServiceCollection services,
        string? inMemoryDatabaseName = "DiagnosticsMetrics")
    {
        var optionsBuilder = new DbContextOptionsBuilder<BaseMetricsDbContext>()
            .UseInMemoryDatabase(inMemoryDatabaseName ?? "DiagnosticsMetrics");

        services.AddSingleton(optionsBuilder.Options);
        services.AddScoped<BaseMetricsDbContext>(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<BaseMetricsDbContext>>();
            return new OtelMetricsDbContext(options);
        });

        return services;
    }
}
