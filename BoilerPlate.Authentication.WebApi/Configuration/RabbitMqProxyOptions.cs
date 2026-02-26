using System.Collections.Concurrent;

namespace BoilerPlate.Authentication.WebApi.Configuration;

/// <summary>
///     Options for the RabbitMQ Management UI proxy. Credentials are derived from RABBITMQ_CONNECTION_STRING.
/// </summary>
public sealed class RabbitMqProxyOptions
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new();

    /// <summary>Base URL for RabbitMQ Management (e.g. http://rabbitmq:15672).</summary>
    public string ManagementBaseUrl { get; set; } = "";

    /// <summary>Username for Basic Auth (from connection string).</summary>
    public string Username { get; set; } = "";

    /// <summary>Password for Basic Auth (from connection string).</summary>
    public string Password { get; set; } = "";

    /// <summary>True if the proxy is configured (ManagementBaseUrl, Username, Password are set).</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ManagementBaseUrl) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);

    /// <summary>Stores an access token for the given duration.</summary>
    public void StoreAccessToken(string token, TimeSpan ttl)
    {
        _tokens[token] = DateTimeOffset.UtcNow.Add(ttl);
    }

    /// <summary>Validates an access token (exists and not expired).</summary>
    public bool ValidateAccessToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        if (!_tokens.TryGetValue(token, out var expiry)) return false;
        if (DateTimeOffset.UtcNow > expiry)
        {
            _tokens.TryRemove(token, out _);
            return false;
        }
        return true;
    }
}
