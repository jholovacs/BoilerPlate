namespace BoilerPlate.Authentication.LdapServer;

/// <summary>
///     Parses LDAP distinguished names to extract username and tenant ID.
/// </summary>
public static class DnParser
{
    /// <summary>
    ///     Parses a bind DN to extract username and optional tenant ID.
    ///     Supports: cn=user,ou=users,ou=&lt;tenant-guid&gt;,dc=... ; uid=user,... ; or simple "username"
    /// </summary>
    public static (string Username, Guid? TenantId) ParseBindDn(string dn, Guid? defaultTenantId)
    {
        if (string.IsNullOrWhiteSpace(dn))
            return (string.Empty, defaultTenantId);

        var normalized = dn.Trim();

        // Simple bind: just username
        if (!normalized.Contains('='))
            return (normalized, defaultTenantId);

        var parts = SplitDn(normalized);
        string? username = null;
        Guid? tenantId = defaultTenantId;

        foreach (var part in parts)
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;

            var attr = part[..eq].Trim().ToLowerInvariant();
            var value = UnescapeDnValue(part[(eq + 1)..].Trim());

            if (attr == "cn" || attr == "uid")
                username ??= value;
            else if (attr == "ou" && Guid.TryParse(value, out var tid))
                tenantId = tid;
        }

        return (username ?? normalized, tenantId);
    }

    private static IEnumerable<string> SplitDn(string dn)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var i = 0;

        while (i < dn.Length)
        {
            var c = dn[i];

            if (c == '\\' && i + 1 < dn.Length)
            {
                current.Append(dn[i + 1]);
                i += 2;
                continue;
            }

            if (c == ',')
            {
                parts.Add(current.ToString());
                current.Clear();
                i++;
                continue;
            }

            current.Append(c);
            i++;
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts;
    }

    private static string UnescapeDnValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                sb.Append(value[i + 1]);
                i++;
            }
            else
            {
                sb.Append(value[i]);
            }
        }
        return sb.ToString();
    }
}
