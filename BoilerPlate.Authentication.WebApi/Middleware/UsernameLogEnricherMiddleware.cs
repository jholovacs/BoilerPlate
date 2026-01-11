using BoilerPlate.Authentication.WebApi.Helpers;
using Serilog.Context;

namespace BoilerPlate.Authentication.WebApi.Middleware;

/// <summary>
///     Middleware to enrich log context with username from authenticated user
/// </summary>
public class UsernameLogEnricherMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UsernameLogEnricherMiddleware> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UsernameLogEnricherMiddleware" /> class
    /// </summary>
    public UsernameLogEnricherMiddleware(
        RequestDelegate next,
        ILogger<UsernameLogEnricherMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    ///     Invokes the middleware to enrich log context with username and user context
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Get user information from authenticated user if available
        string? username = null;
        string? userId = null;
        string? tenantId = null;

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            username = ClaimsHelper.GetUserName(context.User);
            var userIdGuid = ClaimsHelper.GetUserId(context.User);
            if (userIdGuid.HasValue)
                userId = userIdGuid.Value.ToString();
            var tenantIdGuid = ClaimsHelper.GetTenantId(context.User);
            if (tenantIdGuid.HasValue)
                tenantId = tenantIdGuid.Value.ToString();
        }

        // Enrich log context with user information (if available)
        // Use IDisposable pattern for conditional property pushing
        using (LogContext.PushProperty("Username", username ?? "(anonymous)"))
        {
            IDisposable? userIdProperty = userId != null ? LogContext.PushProperty("UserId", userId) : null;
            IDisposable? tenantIdProperty = tenantId != null ? LogContext.PushProperty("TenantId", tenantId) : null;

            try
            {
                await _next(context);
            }
            finally
            {
                userIdProperty?.Dispose();
                tenantIdProperty?.Dispose();
            }
        }
    }
}
