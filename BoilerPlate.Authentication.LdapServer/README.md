# BoilerPlate.Authentication.LdapServer

LDAP server that authenticates against BoilerPlate authentication services and exposes directory data (users, roles). Supports secure communications via LDAPS (TLS).

## Features

- **LDAP Bind**: Validates credentials using `IAuthenticationService` (same as OAuth password grant)
- **LDAP Search**: Returns users and roles from the database as directory entries
- **LDAPS**: TLS-encrypted connections on port 636
- **Multi-tenancy**: DN format `cn=username,ou=users,ou=<tenant-id>,dc=boilerplate,dc=local`

## DN Format

- **Bind**: `cn=username,ou=users,ou=<tenant-guid>,dc=boilerplate,dc=local` or simple `username` (with DefaultTenantId)
- **Search base**: `ou=users,ou=<tenant-guid>,dc=boilerplate,dc=local` or `ou=<tenant-guid>,dc=boilerplate,dc=local`

## Configuration

| Setting | Env (use `__` for nested) | Default | Description |
|---------|---------------------------|---------|-------------|
| Port | LdapServer__Port | 389 | Plain LDAP port (0 to disable) |
| SecurePort | LdapServer__SecurePort | 636 | LDAPS port (0 to disable) |
| BaseDn | LdapServer__BaseDn | dc=boilerplate,dc=local | Base distinguished name |
| DefaultTenantId | LdapServer__DefaultTenantId | - | Tenant GUID when not in DN |
| CertificatePath | LdapServer__CertificatePath | - | Path to PFX/PEM for LDAPS |
| CertificatePassword | LdapServer__CertificatePassword | - | PFX password (if needed) |

## Running

```bash
# From solution root
dotnet run --project BoilerPlate.Authentication.LdapServer.Host -c Release
```

Set `ConnectionStrings__PostgreSqlConnection` or `POSTGRES_CONNECTION_STRING` for the database. Set `LdapServer__DefaultTenantId` to a tenant GUID for simple bind (username format).

## Security

- Use LDAPS (port 636) in production; plain LDAP (389) for development only
- Store certificate and password in secrets (e.g. environment variables)
- The server binds to all interfaces; use firewall rules to restrict access
