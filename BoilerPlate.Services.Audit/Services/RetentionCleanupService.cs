using BoilerPlate.Services.Audit.Models;

namespace BoilerPlate.Services.Audit.Services;

/// <summary>
///     Background service that performs scheduled retention cleanup
/// </summary>
public class RetentionCleanupService : BackgroundService
{
    private readonly RetentionService _retentionService;
    private readonly RetentionConfiguration _config;
    private readonly ILogger<RetentionCleanupService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetentionCleanupService" /> class
    /// </summary>
    public RetentionCleanupService(
        RetentionService retentionService,
        RetentionConfiguration config,
        ILogger<RetentionCleanupService> logger)
    {
        _retentionService = retentionService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    ///     Executes the retention cleanup on a scheduled basis
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Retention cleanup service started. Cleanup frequency: {Frequency}",
            _config.CleanupFrequency);

        // Wait a bit before first run to allow other services to start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting scheduled retention cleanup");
                await _retentionService.PerformRetentionCleanupAsync(stoppingToken);
                _logger.LogInformation("Scheduled retention cleanup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled retention cleanup");
                // Continue running even if cleanup fails
            }

            // Wait for the configured frequency before next cleanup
            try
            {
                await Task.Delay(_config.CleanupFrequency, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is being stopped
                break;
            }
        }

        _logger.LogInformation("Retention cleanup service stopped");
    }
}
