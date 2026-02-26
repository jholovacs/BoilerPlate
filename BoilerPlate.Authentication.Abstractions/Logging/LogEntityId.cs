namespace BoilerPlate.Authentication.Abstractions.Logging;

/// <summary>
///     Formats entity IDs for consistent logging. Use in log messages so the UI can parse and provide drilldown on hover.
///     Format: entityType:id (e.g. "user:550e8400-e29b-41d4-a716-446655440000", "tenant:abc-123").
/// </summary>
public static class LogEntityId
{
    public const string User = "user";
    public const string Tenant = "tenant";
    public const string Role = "role";
    public const string TenantDomain = "tenantDomain";
    public const string TenantVanityUrl = "tenantVanityUrl";
    public const string TenantSetting = "tenantSetting";
    public const string RefreshToken = "refreshToken";
    public const string MfaToken = "mfaToken";
    public const string AuthCode = "authCode";
    public const string Client = "client";

    /// <summary>
    ///     Formats a GUID entity ID for logging.
    /// </summary>
    public static string Format(string entityType, Guid id) => $"{entityType}:{id}";

    /// <summary>
    ///     Formats a string entity ID for logging (e.g. client IDs).
    /// </summary>
    public static string Format(string entityType, string id) => $"{entityType}:{id}";

    public static string UserId(Guid id) => Format(User, id);
    public static string TenantId(Guid id) => Format(Tenant, id);
    public static string RoleId(Guid id) => Format(Role, id);
    public static string TenantDomainId(Guid id) => Format(TenantDomain, id);
    public static string TenantVanityUrlId(Guid id) => Format(TenantVanityUrl, id);
    public static string TenantSettingId(Guid id) => Format(TenantSetting, id);
    public static string RefreshTokenId(Guid id) => Format(RefreshToken, id);
    public static string MfaTokenId(Guid id) => Format(MfaToken, id);
    public static string AuthCodeId(Guid id) => Format(AuthCode, id);
    public static string ClientId(string id) => Format(Client, id);
}
