using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BoilerPlate.Diagnostics.WebApi.Configuration;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Diagnostics.WebApi.Services;

/// <summary>
///     Calls RabbitMQ Management HTTP API for queue/exchange monitoring and basic maintenance.
///     Requires RabbitMQ Management plugin (rabbitmq:3-management image).
/// </summary>
public class RabbitMqManagementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RabbitMqManagementService> _logger;
    private readonly RabbitMqManagementOptions _options;

    public RabbitMqManagementService(
        HttpClient httpClient,
        IOptions<RabbitMqManagementOptions> options,
        ILogger<RabbitMqManagementService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        var baseUrl = (_options.BaseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.LogWarning("RabbitMQ Management BaseUrl is not configured");
            return;
        }

        _httpClient.BaseAddress = new Uri(baseUrl + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(15);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            var auth = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password ?? ""}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", auth);
        }
    }

    /// <summary>Cluster overview (object counts, rates).</summary>
    public async Task<JsonElement?> GetOverviewAsync(CancellationToken ct = default)
    {
        return await GetAsync("/api/overview", ct);
    }

    /// <summary>List queues with message counts, consumers, etc.</summary>
    public async Task<JsonElement?> GetQueuesAsync(CancellationToken ct = default)
    {
        return await GetAsync("/api/queues", ct);
    }

    /// <summary>List exchanges.</summary>
    public async Task<JsonElement?> GetExchangesAsync(CancellationToken ct = default)
    {
        return await GetAsync("/api/exchanges", ct);
    }

    /// <summary>Get a single queue by vhost and name.</summary>
    public async Task<JsonElement?> GetQueueAsync(string vhost, string name, CancellationToken ct = default)
    {
        var encodedVhost = EncodeVhostForRabbitMq(vhost);
        return await GetAsync($"/api/queues/{encodedVhost}/{name}", ct);
    }

    /// <summary>Purge all messages from a queue.</summary>
    public async Task<bool> PurgeQueueAsync(string vhost, string name, CancellationToken ct = default)
    {
        var encodedVhost = EncodeVhostForRabbitMq(vhost);
        return await DeleteAsync($"/api/queues/{encodedVhost}/{name}/contents", ct);
    }

    /// <summary>Get messages from a queue (for troubleshooting). Count limited to 10.</summary>
    public async Task<(JsonElement? Result, string? Error)> GetQueueMessagesWithErrorAsync(string vhost, string name, int count = 10, CancellationToken ct = default)
    {
        var encodedVhost = EncodeVhostForRabbitMq(vhost);
        var body = JsonSerializer.Serialize(new
        {
            count = Math.Min(Math.Max(count, 1), 10),
            ackmode = "ack_requeue_false",
            encoding = "auto"
        });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await PostWithErrorAsync($"/api/queues/{encodedVhost}/{name}/get", content, ct);
    }

    public async Task<JsonElement?> GetQueueMessagesAsync(string vhost, string name, int count = 10, CancellationToken ct = default)
    {
        var (result, _) = await GetQueueMessagesWithErrorAsync(vhost, name, count, ct);
        return result;
    }

    private async Task<JsonElement?> GetAsync(string path, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RabbitMQ Management API {Path} returned {Status}", path, response.StatusCode);
                return null;
            }
            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call RabbitMQ Management API {Path}", path);
            return null;
        }
    }

    private async Task<bool> DeleteAsync(string path, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RabbitMQ Management API DELETE {Path} returned {Status}", path, response.StatusCode);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call RabbitMQ Management API DELETE {Path}", path);
            return false;
        }
    }

    private async Task<JsonElement?> PostAsync(string path, HttpContent content, CancellationToken ct)
    {
        var (result, _) = await PostWithErrorAsync(path, content, ct);
        return result;
    }

    private async Task<(JsonElement? Result, string? Error)> PostWithErrorAsync(string path, HttpContent content, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.PostAsync(path, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RabbitMQ Management API POST {Path} returned {Status}: {Body}", path, response.StatusCode, responseBody);
                return (null, $"{(int)response.StatusCode} {response.StatusCode}: {responseBody}");
            }
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            return (result, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call RabbitMQ Management API POST {Path}", path);
            return (null, ex.Message);
        }
    }

    /// <summary>Encode vhost for RabbitMQ API URL. Default vhost "/" must be "%2F"; avoid double-encoding.</summary>
    private static string EncodeVhostForRabbitMq(string vhost)
    {
        if (string.IsNullOrEmpty(vhost)) return "%2F";
        var decoded = Uri.UnescapeDataString(vhost);
        if (decoded == "/" || string.IsNullOrEmpty(decoded)) return "%2F";
        return Uri.EscapeDataString(decoded);
    }
}
