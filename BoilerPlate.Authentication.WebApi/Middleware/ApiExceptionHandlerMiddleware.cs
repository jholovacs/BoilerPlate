using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace BoilerPlate.Authentication.WebApi.Middleware;

/// <summary>
///     Catches unhandled exceptions and returns a consistent JSON 500 response so the client
///     never receives an empty or broken response (e.g. net::ERR_EMPTY_RESPONSE).
/// </summary>
public class ApiExceptionHandlerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<ApiExceptionHandlerMiddleware> _logger;
    private readonly RequestDelegate _next;

    public ApiExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<ApiExceptionHandlerMiddleware> logger,
        IHostEnvironment hostEnvironment)
    {
        _next = next;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var payload = new
            {
                error = "server_error",
                error_description = "An internal server error occurred. Please try again later.",
                detail = _hostEnvironment.IsDevelopment() ? ex.Message : (string?)null,
                exceptionType = _hostEnvironment.IsDevelopment() ? ex.GetType().Name : (string?)null
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
        }
    }
}
