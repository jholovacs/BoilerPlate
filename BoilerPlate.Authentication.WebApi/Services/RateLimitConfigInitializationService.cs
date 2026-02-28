using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
///     Hosted service that ensures default rate limit configurations exist at startup.
///     Creates configs for oauth/token, jwt/validate, and oauth/authorize with reasonable defaults.
/// </summary>
public class RateLimitConfigInitializationService : IHostedService
{
    private readonly ILogger<RateLimitConfigInitializationService> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Default configurations: endpoint key, permitted requests, window seconds
    /// </summary>
    private static readonly (string Key, int Permitted, int Window)[] Defaults =
    {
        ("oauth/token", 60, 60),
        ("jwt/validate", 60, 60),
        ("oauth/authorize", 120, 60)
    };

    /// <summary>
    ///     Initializes a new instance of the <see cref="RateLimitConfigInitializationService" /> class
    /// </summary>
    public RateLimitConfigInitializationService(IServiceProvider serviceProvider, ILogger<RateLimitConfigInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BaseAuthDbContext>();

        foreach (var (key, permitted, window) in Defaults)
        {
            var exists = await context.RateLimitConfigs.AnyAsync(c => c.EndpointKey == key, cancellationToken);
            if (!exists)
            {
                context.RateLimitConfigs.Add(new RateLimitConfig
                {
                    Id = Guid.NewGuid(),
                    EndpointKey = key,
                    PermittedRequests = permitted,
                    WindowSeconds = window,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                });
                _logger.LogInformation("Created default rate limit config for {Endpoint}: {Permitted} requests per {Window}s", key, permitted, window);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
