using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using BoilerPlate.Authentication.WebApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     Proxy for RabbitMQ Management UI. Service Administrators can obtain access and are automatically
///     logged in via Basic Auth injected by this proxy.
/// </summary>
[ApiController]
[Route("api/rabbitmq")]
public class RabbitMqProxyController : ControllerBase
{
    private const string CookieName = "rabbitmq_proxy_access";
    private const int CookieMaxAgeSeconds = 300; // 5 minutes
    private const string ProxyBasePath = "/api/rabbitmq";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RabbitMqProxyController> _logger;
    private readonly RabbitMqProxyOptions _options;

    public RabbitMqProxyController(
        IHttpClientFactory httpClientFactory,
        RabbitMqProxyOptions options,
        ILogger<RabbitMqProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    ///     Obtains a short-lived access cookie for the RabbitMQ Management proxy.
    ///     Service Administrators only. Call this before opening the proxy URL in a new tab.
    /// </summary>
    /// <response code="200">Returns the URL to open (same origin). Set-Cookie is also returned.</response>
    /// <response code="403">Forbidden - Service Administrator role required</response>
    /// <response code="503">RabbitMQ proxy is not configured</response>
    [HttpGet("access")]
    [Authorize(Policy = AuthorizationPolicies.ServiceAdministrator)]
    [ProducesResponseType(typeof(RabbitMqAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<RabbitMqAccessResponse> GetAccess()
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("RabbitMQ proxy access requested but proxy is not configured");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "RabbitMQ Management proxy is not configured" });
        }

        var token = Guid.NewGuid().ToString("N");
        var cookieOptions = new CookieOptions
        {
            Path = "/api/rabbitmq",
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromSeconds(CookieMaxAgeSeconds)
        };
        Response.Cookies.Append(CookieName, token, cookieOptions);

        _options.StoreAccessToken(token, TimeSpan.FromSeconds(CookieMaxAgeSeconds));

        // Return path only - frontend will prepend its origin (correct host/port/scheme)
        var url = $"{ProxyBasePath}/";
        _logger.LogInformation("RabbitMQ proxy access granted for Service Administrator");
        return Ok(new RabbitMqAccessResponse { Url = url });
    }

    /// <summary>
    ///     Proxies GET requests to RabbitMQ Management UI with Basic Auth. Requires valid access cookie.
    /// </summary>
    [HttpGet("{**path}")]
    [ResponseCache(NoStore = true)]
    public Task<IActionResult> ProxyGet(string? path, CancellationToken cancellationToken) =>
        Proxy(HttpMethod.Get, path, null, cancellationToken);

    /// <summary>
    ///     Proxies POST requests to RabbitMQ Management API. Requires valid access cookie.
    /// </summary>
    [HttpPost("{**path}")]
    [ResponseCache(NoStore = true)]
    public Task<IActionResult> ProxyPost(string? path, CancellationToken cancellationToken) =>
        Proxy(HttpMethod.Post, path, Request.Body, cancellationToken);

    /// <summary>
    ///     Proxies PUT requests to RabbitMQ Management API. Requires valid access cookie.
    /// </summary>
    [HttpPut("{**path}")]
    [ResponseCache(NoStore = true)]
    public Task<IActionResult> ProxyPut(string? path, CancellationToken cancellationToken) =>
        Proxy(HttpMethod.Put, path, Request.Body, cancellationToken);

    /// <summary>
    ///     Proxies DELETE requests to RabbitMQ Management API. Requires valid access cookie.
    /// </summary>
    [HttpDelete("{**path}")]
    [ResponseCache(NoStore = true)]
    public Task<IActionResult> ProxyDelete(string? path, CancellationToken cancellationToken) =>
        Proxy(HttpMethod.Delete, path, null, cancellationToken);

    private async Task<IActionResult> Proxy(HttpMethod method, string? path, Stream? body, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        if (!Request.Cookies.TryGetValue(CookieName, out var token) || !_options.ValidateAccessToken(token))
        {
            return Unauthorized(new { error = "Invalid or expired RabbitMQ proxy access. Please use the RabbitMQ Management link again." });
        }

        var rabbitPath = string.IsNullOrEmpty(path) ? "" : path.TrimStart('/');
        var targetUrl = $"{_options.ManagementBaseUrl.TrimEnd('/')}/{rabbitPath}";
        if (Request.QueryString.HasValue)
            targetUrl += Request.QueryString.Value;

        var client = _httpClientFactory.CreateClient("RabbitMqProxy");
        var request = new HttpRequestMessage(method, targetUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}")));

        if (Request.ContentLength > 0 && body != null)
        {
            request.Content = new StreamContent(body);
            if (Request.ContentType != null)
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(Request.ContentType);
        }

        try
        {
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var needsRewrite = method == HttpMethod.Get &&
                (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
                              contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                              contentType.Contains("application/javascript", StringComparison.OrdinalIgnoreCase) ||
                              contentType.Contains("text/css", StringComparison.OrdinalIgnoreCase));

            if (needsRewrite && content.Length > 0)
            {
                var text = Encoding.UTF8.GetString(content);
                text = RewritePaths(text);
                content = Encoding.UTF8.GetBytes(text);
            }

            return File(content, contentType);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to proxy request to RabbitMQ Management at {Url}", targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Unable to reach RabbitMQ Management" });
        }
    }

    private static string RewritePaths(string content)
    {
        // RabbitMQ Management UI uses absolute paths. Rewrite so they go through our proxy.
        var prefix = ProxyBasePath;
        content = Regex.Replace(content, @"<head>", $"<head><base href=\"{prefix}/\">", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"href=""/", $"href=\"{prefix}/");
        content = Regex.Replace(content, @"src=""/", $"src=\"{prefix}/");
        content = Regex.Replace(content, @"""\/api\/", $"\"{prefix}/api/");
        content = Regex.Replace(content, @"'\/api\/", $"'{prefix}/api/");
        content = Regex.Replace(content, @"url\(""/", $"url(\"{prefix}/");
        content = Regex.Replace(content, @"url\('/", $"url('{prefix}/");
        content = Regex.Replace(content, @"action=""/", $"action=\"{prefix}/");
        return content;
    }
}

/// <summary>Response from the RabbitMQ proxy access endpoint.</summary>
public record RabbitMqAccessResponse
{
    /// <summary>URL to open in a new tab. Use window.open(url) after this request (cookie will be set).</summary>
    public required string Url { get; init; }
}
