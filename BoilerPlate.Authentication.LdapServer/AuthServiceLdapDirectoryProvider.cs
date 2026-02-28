using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.LdapServer.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.LdapServer;

/// <summary>
///     LDAP directory provider that uses IAuthenticationService for bind and IUserService for search.
/// </summary>
public class AuthServiceLdapDirectoryProvider : ILdapDirectoryProvider
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserService _userService;
    private readonly LdapServerOptions _options;
    private readonly ILogger<AuthServiceLdapDirectoryProvider> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthServiceLdapDirectoryProvider" /> class
    /// </summary>
    public AuthServiceLdapDirectoryProvider(
        IAuthenticationService authenticationService,
        IUserService userService,
        IOptions<LdapServerOptions> options,
        ILogger<AuthServiceLdapDirectoryProvider> logger)
    {
        _authenticationService = authenticationService;
        _userService = userService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!tenantId.HasValue)
        {
            _logger.LogDebug("LDAP bind failed: tenant ID required");
            return false;
        }

        var loginRequest = new LoginRequest
        {
            TenantId = tenantId,
            UserNameOrEmail = username,
            Password = password,
            RememberMe = false
        };

        var result = await _authenticationService.LoginAsync(loginRequest, cancellationToken);

        if (result.Succeeded)
            _logger.LogDebug("LDAP bind succeeded for user {Username} in tenant {TenantId}", username, tenantId);
        else
            _logger.LogDebug("LDAP bind failed for user {Username} in tenant {TenantId}", username, tenantId);

        return result.Succeeded;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LdapDirectoryEntry>> SearchAsync(
        Guid tenantId,
        string? filterAttribute,
        string? filterValue,
        CancellationToken cancellationToken = default)
    {
        var users = await _userService.GetAllUsersAsync(tenantId, cancellationToken);
        var entries = users.Select(MapToEntry).ToList();

        if (string.IsNullOrEmpty(filterAttribute) || string.IsNullOrEmpty(filterValue))
            return entries;

        var value = filterValue.Trim();
        var wildcard = value.Contains('*');

        return entries.Where(e =>
        {
            var attrValue = GetAttributeValue(e, filterAttribute);
            if (string.IsNullOrEmpty(attrValue)) return false;
            if (wildcard)
                return MatchesWildcard(attrValue, value);
            return string.Equals(attrValue, value, StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LdapDirectoryEntry>> GetAllUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var users = await _userService.GetAllUsersAsync(tenantId, cancellationToken);
        return users.Select(MapToEntry).ToList();
    }

    private LdapDirectoryEntry MapToEntry(UserDto u)
    {
        var cn = u.UserName;
        var baseDn = _options.BaseDn;
        var dn = $"cn={EscapeDnValue(cn)},ou=users,ou={u.TenantId},{baseDn}";
        var displayName = string.IsNullOrEmpty(u.FirstName) && string.IsNullOrEmpty(u.LastName)
            ? u.UserName
            : $"{u.FirstName} {u.LastName}".Trim();

        return new LdapDirectoryEntry
        {
            DistinguishedName = dn,
            Cn = cn,
            Uid = u.UserName,
            SamAccountName = u.UserName,
            Mail = u.Email,
            DisplayName = displayName,
            GivenName = u.FirstName,
            Sn = u.LastName,
            UserId = u.Id,
            TenantId = u.TenantId,
            MemberOf = u.Roles.Select(r => $"cn={EscapeDnValue(r)},ou=roles,ou={u.TenantId},{_options.BaseDn}").ToList()
        };
    }

    private static string? GetAttributeValue(LdapDirectoryEntry e, string attr)
    {
        return attr.ToLowerInvariant() switch
        {
            "cn" => e.Cn,
            "uid" => e.Uid,
            "samaccountname" => e.SamAccountName,
            "mail" => e.Mail,
            "displayname" => e.DisplayName,
            "givenname" => e.GivenName,
            "sn" => e.Sn,
            _ => null
        };
    }

    private static bool MatchesWildcard(string value, string pattern)
    {
        if (pattern == "*") return true;
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(value, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string EscapeDnValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace("+", "\\+")
            .Replace("\"", "\\\"")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace(";", "\\;");
    }
}
