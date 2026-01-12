using Npgsql;

namespace BoilerPlate.Services.Audit.Services;

/// <summary>
///     Provider for accessing tenant settings from PostgreSQL database
/// </summary>
public class TenantSettingsProvider : ITenantSettingsProvider
{
    private readonly string _connectionString;
    private readonly ILogger<TenantSettingsProvider> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TenantSettingsProvider" /> class
    /// </summary>
    public TenantSettingsProvider(
        string connectionString,
        ILogger<TenantSettingsProvider> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> GetTenantSettingAsync(Guid tenantId, string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var command = new NpgsqlCommand(
                "SELECT \"Value\" FROM \"TenantSettings\" WHERE \"TenantId\" = @tenantId AND \"Key\" = @key",
                connection);

            command.Parameters.AddWithValue("tenantId", tenantId);
            command.Parameters.AddWithValue("key", key);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tenant setting {Key} for tenant {TenantId}", key, tenantId);
            return null;
        }
    }
}
