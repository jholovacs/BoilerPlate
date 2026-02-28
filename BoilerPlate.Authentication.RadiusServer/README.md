# BoilerPlate.Authentication.RadiusServer

RADIUS server that authenticates against BoilerPlate authentication services for network access (VPN, WiFi, etc.).

## Features

- **Access-Request**: Validates credentials using `IAuthenticationService` (same as OAuth password grant)
- **PAP**: Password Authentication Protocol (User-Name, User-Password)
- **Multi-tenancy**: Default tenant from config, or realm in username (`user@tenant-guid`)

## Configuration

| Setting | Env (use `__` for nested) | Default | Description |
|--------|---------------------------|---------|-------------|
| Port | RadiusServer__Port | 11812 | RADIUS auth port (11812 for non-privileged in containers) |
| SharedSecret | RadiusServer__SharedSecret | radsec | Shared secret for RADIUS clients (NAS) |
| DefaultTenantId | RadiusServer__DefaultTenantId | - | Tenant GUID when not in username |

## Running

```bash
# From solution root
dotnet run --project BoilerPlate.Authentication.RadiusServer.Host -c Release
```

Set `ConnectionStrings__PostgreSqlConnection` for the database. Set `RadiusServer__DefaultTenantId` to a tenant GUID for simple username format.

## Testing

Use `radtest` (from FreeRADIUS) or similar:

```bash
radtest username password localhost:11812 0 radsec
```

## Security

- Use a strong shared secret in production (`RADIUS_SHARED_SECRET`)
- Restrict which NAS IPs can connect (configure per-client secrets if needed)
- RADIUS uses UDP; ensure firewall rules allow only trusted clients
