# Authentication Web API

OAuth2 authentication API with JWT tokens using RS256 (RSA asymmetric encryption) for PQC-resistant signatures.

## Features

- OAuth2 Resource Owner Password Credentials grant
- JWT token generation with RS256 (RSA-2048) asymmetric encryption
- Multi-tenant support
- **Multi-Factor Authentication (MFA)** with TOTP support
- RESTful API endpoints
- Swagger/OpenAPI documentation

## Setup

### 1. Generate RSA Key Pair

For production, you need to generate an RSA key pair (2048-bit or higher recommended for quantum resistance). The following methods show how to generate keys using OpenSSL.

#### Method 1: Generate Unencrypted Keys (Recommended for Production with Secrets Manager)

```bash
# Generate a 2048-bit RSA private key (unencrypted)
openssl genrsa -out private_key.pem 2048

# Extract the public key from the private key
openssl rsa -in private_key.pem -pubout -out public_key.pem

# Verify the keys were generated correctly
openssl rsa -in private_key.pem -text -noout
openssl rsa -in public_key.pem -pubin -text -noout
```

#### Method 2: Generate Encrypted Keys (Then Decrypt for Use)

If you need to generate an encrypted key for storage, then decrypt it before use:

```bash
# Generate an encrypted 2048-bit RSA private key (will prompt for password)
openssl genrsa -aes256 -out private_key_encrypted.pem 2048

# Decrypt the key for use in the application
openssl rsa -in private_key_encrypted.pem -out private_key.pem

# Extract the public key
openssl rsa -in private_key.pem -pubout -out public_key.pem
```

#### Method 3: Generate Larger Keys (4096-bit for Enhanced Security)

For enhanced security, you can generate 4096-bit keys:

```bash
# Generate a 4096-bit RSA private key
openssl genrsa -out private_key_4096.pem 4096

# Extract the public key
openssl rsa -in private_key_4096.pem -pubout -out public_key_4096.pem
```

#### Key Format Requirements

The application expects keys in **PEM format**. Your keys should look like this:

**Private Key Format:**
```
-----BEGIN RSA PRIVATE KEY-----
MIIEpAIBAAKCAQEA...
(multiple lines of base64 encoded data)
...
-----END RSA PRIVATE KEY-----
```

**Public Key Format:**
```
-----BEGIN RSA PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A...
(multiple lines of base64 encoded data)
...
-----END RSA PUBLIC KEY-----
```

#### Converting Keys to Single-Line Format (for Environment Variables)

When using environment variables, you may need to convert multi-line keys to single-line format:

**Windows PowerShell:**
```powershell
# Read the key and replace newlines with \n
$key = Get-Content private_key.pem -Raw
$key = $key -replace "`r`n", "\n"
$env:JWT_PRIVATE_KEY = $key
```

**Linux/macOS:**
```bash
# Use tr to replace newlines with \n
export JWT_PRIVATE_KEY=$(cat private_key.pem | tr '\n' '\n')
# Or use printf for better control
export JWT_PRIVATE_KEY=$(printf '%s\n' "$(cat private_key.pem)")
```

#### Viewing Key Information

To verify your keys and view their details:

```bash
# View private key information (without displaying the key itself)
openssl rsa -in private_key.pem -text -noout | head -20

# View public key information
openssl rsa -in public_key.pem -pubin -text -noout

# Check key size
openssl rsa -in private_key.pem -text -noout | grep "Private-Key"
```

#### Security Best Practices

1. **Never commit keys to source control**: Add `*.pem`, `*_key.pem`, `*_key_*.pem` to your `.gitignore`
2. **Use secrets managers in production**: Store keys in Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, etc.
3. **Set proper file permissions** (Linux/macOS):
   ```bash
   chmod 600 private_key.pem  # Read/write for owner only
   chmod 644 public_key.pem   # Readable by all (public key is safe to share)
   ```
4. **Rotate keys periodically**: Generate new keys and update your configuration
5. **Use environment variables or secrets managers**: Avoid hardcoding keys in configuration files
6. **For encrypted keys**: Decrypt them before use and store the decrypted version securely in a secrets manager

#### Example: Complete Key Generation Workflow

```bash
# 1. Generate the private key
openssl genrsa -out jwt_private_key.pem 2048

# 2. Extract the public key
openssl rsa -in jwt_private_key.pem -pubout -out jwt_public_key.pem

# 3. Verify the keys
echo "Private Key:"
openssl rsa -in jwt_private_key.pem -check -noout
echo "Public Key:"
openssl rsa -in jwt_public_key.pem -pubin -check -noout

# 4. View key details (optional)
openssl rsa -in jwt_private_key.pem -text -noout | grep -E "Private-Key|modulus|publicExponent"

# 5. Set proper permissions (Linux/macOS)
chmod 600 jwt_private_key.pem
chmod 644 jwt_public_key.pem

# 6. Copy keys to your configuration or secrets manager
# For appsettings.json, copy the entire content including BEGIN/END markers
# For environment variables, use the methods shown above
```

### 2. Configure appsettings.json

Add your RSA keys to `appsettings.json`:

```json
{
  "JwtSettings": {
    "Issuer": "BoilerPlate.Authentication",
    "Audience": "BoilerPlate.API",
    "ExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7,
    "PrivateKey": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----",
    "PublicKey": "-----BEGIN RSA PUBLIC KEY-----\n...\n-----END RSA PUBLIC KEY-----"
  }
}
```

**Note:** For development, if keys are not provided, the system will auto-generate them (not recommended for production).

### 3. Environment Variables

The following environment variables can be used to configure the authentication service:

#### JWT Configuration

- **`JWT_EXPIRATION_MINUTES`** - JWT token expiration time in minutes (default: 15 minutes)
  - Example: `JWT_EXPIRATION_MINUTES=30` sets tokens to expire in 30 minutes
  - This overrides the `ExpirationMinutes` value in `appsettings.json`

- **`JWT_PRIVATE_KEY`** - RSA private key in PEM format for signing JWT tokens
  - This overrides the `PrivateKey` value in `appsettings.json`
  - **Recommended for Docker/Containerized environments**: Base64-encoded PEM format
    - The application automatically detects and decodes base64-encoded keys
    - Base64-encoded keys avoid issues with newlines and special characters in environment variables
    - Example (PowerShell): `[Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content private_key.pem -Raw)))`
    - Example (Linux/macOS): `cat private_key.pem | base64 | tr -d '\n'`
  - **Also supported**: Plain PEM format with `-----BEGIN RSA PRIVATE KEY-----...-----END RSA PRIVATE KEY-----` markers
    - When using plain PEM format, ensure newlines are preserved or use `\n` escape sequences
  - **Important**: The key must be unencrypted. If you have an encrypted key, decrypt it first using OpenSSL:
    ```bash
    openssl rsa -in encrypted_key.pem -out decrypted_key.pem
    ```
  - For production, use a secrets manager (Azure Key Vault, AWS Secrets Manager, etc.) to securely store and inject the key
  - **Security Note**: Never commit private keys to source control or log them

- **`JWT_PUBLIC_KEY`** - RSA public key in PEM format for validating JWT tokens
  - This overrides the `PublicKey` value in `appsettings.json`
  - **Recommended for Docker/Containerized environments**: Base64-encoded PEM format (same as private key)
  - **Also supported**: Plain PEM format with `-----BEGIN RSA PUBLIC KEY-----...-----END RSA PUBLIC KEY-----` markers
  - If only the public key is provided (and no private key), the service can validate tokens but cannot generate new ones

- **`JWT_PRIVATE_KEY_PASSWORD`** - Password for the private key (if encrypted)
  - **Note**: Currently, .NET's `ImportFromPem` method does not support password-protected PEM keys directly
  - If you have an encrypted key, you must decrypt it first before providing it via `JWT_PRIVATE_KEY`
  - This environment variable is reserved for future use or custom decryption implementations
  - For security, decrypt keys using OpenSSL and store the decrypted key in a secrets manager

#### Admin User Configuration

- **`ADMIN_USERNAME`** - Username for the service administrator account (required for admin user initialization)
- **`ADMIN_PASSWORD`** - Password for the service administrator account (required for admin user initialization)
- **`ADMIN_TENANT_ID`** - Optional tenant ID (UUID) for the admin user. If not specified, a "System" tenant will be created/used

#### MongoDB Logging Configuration

- **`MONGODB_CONNECTION_STRING`** - **REQUIRED** MongoDB connection string for Serilog logging
  - The application uses Serilog to write all logs to a MongoDB collection named "logs"
  - Connection string format: `mongodb://username:password@host:port/database` or `mongodb://host:port/database`
  - The database name is parsed from the connection string. If not specified in the connection string, it defaults to "logs"
  - A timestamp index is automatically created on the "logs" collection on application startup
  - **Examples:**
    - Local MongoDB: `mongodb://localhost:27017/logs`
    - With credentials: `mongodb://admin:password@localhost:27017/logs`
    - MongoDB Atlas: `mongodb+srv://username:password@cluster.mongodb.net/logs`
    - Custom database: `mongodb://localhost:27017/myapp_logs`
  - **Windows PowerShell:**
    ```powershell
    $env:MONGODB_CONNECTION_STRING="mongodb://localhost:27017/logs"
    ```
  - **Linux/macOS:**
    ```bash
    export MONGODB_CONNECTION_STRING="mongodb://localhost:27017/logs"
    ```
  - **Docker:**
    ```bash
    docker run -e MONGODB_CONNECTION_STRING="mongodb://mongo:27017/logs" ...
    ```
  - **Kubernetes Secret:**
    ```yaml
    apiVersion: v1
    kind: Secret
    metadata:
      name: mongodb-config
    type: Opaque
    stringData:
      MONGODB_CONNECTION_STRING: "mongodb://username:password@mongodb-service:27017/logs"
    ```
  - **Note**: If this environment variable is not set, the application will fail to start with a clear error message

#### RabbitMQ Service Bus Configuration

- **`RABBITMQ_CONNECTION_STRING`** - Optional RabbitMQ connection string for asynchronous messaging
  - If provided, overrides the connection string in `appsettings.json`
  - Connection string format: `amqp://username:password@host:port/vhost` or `amqp://username:password@host:port/`
  - **Examples:**
    - Default (guest/guest): `amqp://guest:guest@localhost:5672/`
    - With credentials: `amqp://admin:password@localhost:5672/`
    - With virtual host: `amqp://admin:password@localhost:5672/my_vhost`
  - **Windows PowerShell:**
    ```powershell
    $env:RABBITMQ_CONNECTION_STRING="amqp://guest:guest@localhost:5672/"
    ```
  - **Linux/macOS:**
    ```bash
    export RABBITMQ_CONNECTION_STRING="amqp://guest:guest@localhost:5672/"
    ```
  - **Note**: If not provided, the application will use the connection string from `appsettings.json` (defaults to `amqp://guest:guest@localhost:5672/`)

#### Example Environment Variable Setup

**Using Base64-Encoded Keys (Recommended for Docker/Containerized environments):**

```powershell
# Windows PowerShell
$env:JWT_EXPIRATION_MINUTES="30"
# Base64 encode the keys (recommended for Docker)
$env:JWT_PRIVATE_KEY=[Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content jwt-keys/private_key.pem -Raw)))
$env:JWT_PUBLIC_KEY=[Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content jwt-keys/public_key.pem -Raw)))
$env:ADMIN_USERNAME="admin"
$env:ADMIN_PASSWORD="SecurePassword123!"
$env:MONGODB_CONNECTION_STRING="mongodb://localhost:27017/logs"
$env:RABBITMQ_CONNECTION_STRING="amqp://guest:guest@localhost:5672/"
```

```bash
# Linux/macOS
export JWT_EXPIRATION_MINUTES=30
# Base64 encode the keys (recommended for Docker)
export JWT_PRIVATE_KEY="$(cat jwt-keys/private_key.pem | base64 | tr -d '\n')"
export JWT_PUBLIC_KEY="$(cat jwt-keys/public_key.pem | base64 | tr -d '\n')"
export ADMIN_USERNAME=admin
export ADMIN_PASSWORD=SecurePassword123!
export MONGODB_CONNECTION_STRING="mongodb://localhost:27017/logs"
export RABBITMQ_CONNECTION_STRING="amqp://guest:guest@localhost:5672/"
```

**Using Plain PEM Format (Alternative):**

```powershell
# Windows PowerShell - Using plain PEM format with escape sequences
$env:JWT_EXPIRATION_MINUTES="30"
$env:JWT_PRIVATE_KEY="-----BEGIN RSA PRIVATE KEY-----`nMIIEpAIBAAKCAQEA...`n-----END RSA PRIVATE KEY-----"
$env:JWT_PUBLIC_KEY="-----BEGIN RSA PUBLIC KEY-----`nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A...`n-----END RSA PUBLIC KEY-----"
# ... other variables ...
```

```bash
# Linux/macOS - Using plain PEM format with actual newlines
export JWT_EXPIRATION_MINUTES=30
export JWT_PRIVATE_KEY="-----BEGIN RSA PRIVATE KEY-----
MIIEpAIBAAKCAQEA...
-----END RSA PRIVATE KEY-----"
export JWT_PUBLIC_KEY="-----BEGIN RSA PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A...
-----END RSA PUBLIC KEY-----"
# ... other variables ...
```

#### Using Environment Variables with Docker/Kubernetes

For containerized deployments, you can inject keys via environment variables:

```yaml
# Kubernetes Secret (base64 encoded)
apiVersion: v1
kind: Secret
metadata:
  name: jwt-keys
type: Opaque
stringData:
  JWT_PRIVATE_KEY: |
    -----BEGIN RSA PRIVATE KEY-----
    ...
    -----END RSA PRIVATE KEY-----
  JWT_PUBLIC_KEY: |
    -----BEGIN RSA PUBLIC KEY-----
    ...
    -----END RSA PUBLIC KEY-----
```

```yaml
# Kubernetes Deployment
env:
  - name: JWT_PRIVATE_KEY
    valueFrom:
      secretKeyRef:
        name: jwt-keys
        key: JWT_PRIVATE_KEY
  - name: JWT_PUBLIC_KEY
    valueFrom:
      secretKeyRef:
        name: jwt-keys
        key: JWT_PUBLIC_KEY
```

#### Priority Order

Configuration values are loaded in the following priority order (highest to lowest):
1. Environment variables (`JWT_PRIVATE_KEY`, `JWT_PUBLIC_KEY`, `JWT_EXPIRATION_MINUTES`, `MONGODB_CONNECTION_STRING`, `RABBITMQ_CONNECTION_STRING`)
2. `appsettings.json` configuration file
3. Default values (for expiration minutes and RabbitMQ connection string only)

**Note**: `MONGODB_CONNECTION_STRING` is **required** and must be provided as an environment variable. The application will not start without it.

### 3. Database Connection

Configure your database connection string in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;..."
  }
}
```

### 4. MongoDB Logging Setup

The application uses Serilog with MongoDB sink to write all application logs to a MongoDB collection.

#### Collection Details

- **Collection Name**: `logs`
- **Database**: Parsed from `MONGODB_CONNECTION_STRING` (defaults to "logs" if not specified)
- **Indexed Field**: `Timestamp` (descending order for most recent first)
- **Index Creation**: Automatic on application startup

#### Log Collection Structure

Each log entry in MongoDB contains:
- `Timestamp` - ISO 8601 formatted timestamp (indexed)
- `Level` - Log level (Information, Warning, Error, etc.)
- `Message` - Log message
- `Exception` - Exception details (if applicable)
- `Properties` - Additional structured properties
  - `Application` - Application name ("BoilerPlate.Authentication.WebApi")
  - Other contextual properties from log context

#### Log Levels

- **Information**: General application flow and informational messages
- **Warning**: Warnings and non-critical issues
- **Error**: Errors and exceptions
- **Fatal**: Critical errors that cause application shutdown

Microsoft.AspNetCore logs are filtered to Warning level and above to reduce noise.

#### Example MongoDB Query

To query logs by timestamp (using the index):

```javascript
// Most recent logs first (uses index)
db.logs.find().sort({ Timestamp: -1 }).limit(100)

// Logs in a time range
db.logs.find({
  Timestamp: {
    $gte: ISODate("2025-01-01T00:00:00Z"),
    $lte: ISODate("2025-01-31T23:59:59Z")
  }
}).sort({ Timestamp: -1 })

// Error logs only
db.logs.find({ Level: "Error" }).sort({ Timestamp: -1 })
```

## API Endpoints

### POST /oauth/token

OAuth2 token endpoint - Issues JWT tokens upon successful authentication.

**Request:**
```json
{
  "grant_type": "password",
  "username": "user@example.com",
  "password": "password123",
  "tenant_id": "00000000-0000-0000-0000-000000000000",
  "scope": "read write"
}
```

**Response:**
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "...",
  "scope": "read write"
}
```

### POST /oauth/refresh

Refresh access token using refresh token (not yet implemented - requires database storage).

### GET /.well-known/jwks.json

Returns the public key in JWKS format for JWT validation.

## Using the JWT Token

Include the token in the Authorization header:

```
Authorization: Bearer <access_token>
```

## Security Notes

- **RS256 (RSA-2048)** is used for JWT signatures, providing asymmetric encryption
- RSA-2048 provides quantum resistance for current threat models
- For enhanced PQC resistance, consider upgrading to RSA-4096 or implementing Dilithium/Falcon algorithms when available
- Private keys should be stored securely (Azure Key Vault, AWS Secrets Manager, etc.)
- Never commit private keys to source control

## SAML2 Identity Provider Configuration

This solution supports SAML2 as an Identity Provider (IdP) at the tenant level, allowing each tenant to configure their own SAML2 SSO settings. This enables Single Sign-On (SSO) integration with external Service Providers (SPs).

### Overview

SAML2 SSO allows users to authenticate once with an Identity Provider and access multiple Service Provider applications without re-entering credentials. In this solution:

- **This application acts as the Identity Provider (IdP)**: Users authenticate here and are redirected to Service Provider applications
- **Each tenant can have its own SAML2 configuration**: Different tenants can use different Service Providers
- **Settings are stored per tenant**: All SAML2 settings are stored in the `TenantSettings` table with keys prefixed with `saml2.`

### Prerequisites

1. **Service Provider (SP) Information**: You need the following information from the Service Provider:
   - SP Entity ID (unique identifier for the SP)
   - SP Assertion Consumer Service (ACS) URL (where SAML responses are sent)
   - SP certificate (for verifying signed requests, if required)

2. **X.509 Certificates**: You'll need certificates for:
   - **IdP Certificate** (this application): Used to sign SAML responses sent to Service Providers
   - **SP Certificate**: Used to verify signed authentication requests from Service Providers (if required)

### Step 1: Generate X.509 Certificates

#### Generate IdP Certificate (for signing SAML responses)

```bash
# Generate a self-signed certificate for the IdP (valid for 1 year)
openssl req -x509 -newkey rsa:2048 -keyout idp_private_key.pem -out idp_certificate.pem -days 365 -nodes -subj "/CN=your-idp-entity-id/O=Your Organization"

# For production, use a certificate from a trusted Certificate Authority (CA)
# or your organization's PKI infrastructure
```

#### Convert Certificate to Base64

For storing in tenant settings, convert the certificate to base64:

```bash
# Linux/macOS
cat idp_certificate.pem | base64 | tr -d '\n'

# Windows PowerShell
[Convert]::ToBase64String([System.IO.File]::ReadAllBytes("idp_certificate.pem"))
```

**Note**: The certificate should include the full certificate chain if using a CA-signed certificate.

### Step 2: Configure SAML2 Settings for a Tenant

Use the REST API to configure SAML2 settings for a tenant. You'll need a valid JWT token with appropriate permissions (Service Administrator or Tenant Administrator).

#### API Endpoint

```
POST /saml2/settings/{tenantId}
```

#### Request Body

```json
{
  "isEnabled": true,
  "idpEntityId": "https://your-app.com/saml2",
  "idpSsoServiceUrl": "https://your-app.com/saml2/sso/{tenantId}",
  "idpCertificate": "base64-encoded-certificate",
  "spEntityId": "https://sp-application.com",
  "spAcsUrl": "https://sp-application.com/saml/acs",
  "spCertificate": "base64-encoded-sp-certificate",
  "nameIdFormat": "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
  "signAuthnRequest": true,
  "requireSignedResponse": true,
  "requireEncryptedAssertion": false,
  "clockSkewMinutes": 5
}
```

#### Configuration Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| `isEnabled` | Yes | Set to `true` to enable SAML2 SSO for this tenant |
| `idpEntityId` | Yes | Unique identifier for this Identity Provider (e.g., `https://your-app.com/saml2`) |
| `idpSsoServiceUrl` | Yes | URL where Service Providers initiate SSO (typically `https://your-app.com/saml2/sso/{tenantId}`) |
| `idpCertificate` | Yes | Base64-encoded X.509 certificate for signing SAML responses |
| `spEntityId` | Yes | Service Provider's entity ID (provided by the SP) |
| `spAcsUrl` | Yes | Service Provider's Assertion Consumer Service URL (where SAML responses are sent) |
| `spCertificate` | Recommended | Base64-encoded X.509 certificate from the Service Provider (for verifying signed requests) |
| `spCertificatePrivateKey` | Optional | Private key for SP certificate (if certificate doesn't include private key) |
| `nameIdFormat` | Optional | Name identifier format (default: `urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress`) |
| `attributeMapping` | Optional | JSON mapping of SAML attributes to user properties |
| `signAuthnRequest` | Optional | Whether to sign authentication requests (default: `true`) |
| `requireSignedResponse` | Optional | Whether to require signed SAML responses (default: `true`) |
| `requireEncryptedAssertion` | Optional | Whether to require encrypted assertions (default: `false`) |
| `clockSkewMinutes` | Optional | Clock skew tolerance in minutes (default: `5`) |

#### Example: Configure SAML2 for a Tenant

```bash
# Using curl
curl -X POST "https://your-app.com/saml2/settings/123e4567-e89b-12d3-a456-426614174000" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isEnabled": true,
    "idpEntityId": "https://your-app.com/saml2",
    "idpSsoServiceUrl": "https://your-app.com/saml2/sso/123e4567-e89b-12d3-a456-426614174000",
    "idpCertificate": "LS0tLS1CRUdJTiBDRVJUSUZJQ0FURS0tLS0t...",
    "spEntityId": "https://sp-application.com",
    "spAcsUrl": "https://sp-application.com/saml/acs",
    "spCertificate": "LS0tLS1CRUdJTiBDRVJUSUZJQ0FURS0tLS0t...",
    "nameIdFormat": "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
    "signAuthnRequest": true,
    "requireSignedResponse": true
  }'
```

### Step 3: Provide IdP Metadata to Service Provider

Service Providers typically need IdP metadata to configure the SAML2 integration. You can provide them with:

1. **IdP Entity ID**: The value you configured in `idpEntityId`
2. **SSO Service URL**: The value you configured in `idpSsoServiceUrl`
3. **IdP Certificate**: The public certificate (they need the public key, not the private key)

#### Generate IdP Metadata XML (Optional)

Some Service Providers require metadata in XML format. You can generate this manually or use SAML2 metadata generators:

```xml
<?xml version="1.0"?>
<EntityDescriptor xmlns="urn:oasis:names:tc:SAML:2.0:metadata"
                  entityID="https://your-app.com/saml2">
  <IDPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol">
    <KeyDescriptor use="signing">
      <KeyInfo xmlns="http://www.w3.org/2000/09/xmldsig#">
        <X509Data>
          <X509Certificate>
            <!-- Base64-encoded certificate (without BEGIN/END markers) -->
          </X509Certificate>
        </X509Data>
      </KeyInfo>
    </KeyDescriptor>
    <SingleSignOnService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect"
                        Location="https://your-app.com/saml2/sso/{tenantId}"/>
  </IDPSSODescriptor>
</EntityDescriptor>
```

### Step 4: SAML2 SSO Flow

Once configured, the SAML2 SSO flow works as follows:

1. **User initiates SSO**: User clicks a link or is redirected to the Service Provider
2. **SP redirects to IdP**: Service Provider redirects user to `/saml2/sso/{tenantId}`
3. **User authenticates**: If not already authenticated, user logs in with username/password
4. **IdP generates SAML response**: System creates a SAML assertion with user information
5. **IdP redirects to SP**: User is redirected to SP's ACS URL with SAML response
6. **SP validates and logs in user**: Service Provider validates the SAML response and creates a session

#### SSO Initiation Endpoint

```
GET /saml2/sso/{tenantId}?returnUrl=https://sp-application.com/callback
```

**Parameters:**
- `tenantId` (path): The tenant ID (UUID)
- `returnUrl` (query, optional): URL to redirect to after successful authentication

#### Assertion Consumer Service (ACS) Endpoint

```
POST /saml2/acs/{tenantId}
```

This endpoint receives SAML responses from Service Providers (if using SP-initiated SSO). The endpoint processes the SAML response and returns a JWT token.

### Step 5: Testing SAML2 Configuration

#### Test SSO Initiation

1. **Get a JWT token** (if testing programmatically):
   ```bash
   curl -X POST "https://your-app.com/oauth/token" \
     -H "Content-Type: application/json" \
     -d '{
       "grant_type": "password",
       "username": "testuser@example.com",
       "password": "password123"
     }'
   ```

2. **Initiate SSO**:
   ```bash
   curl -L "https://your-app.com/saml2/sso/123e4567-e89b-12d3-a456-426614174000?returnUrl=https://sp-application.com/callback"
   ```

3. **Verify redirect**: You should be redirected to the Service Provider's ACS URL with a SAML response.

#### Common Issues and Troubleshooting

1. **"SAML2 SSO is not configured or enabled for this tenant"**
   - Verify that `isEnabled` is set to `true` in the tenant's SAML2 settings
   - Check that all required settings are configured

2. **"SAML2 configuration is incomplete"**
   - Ensure `idpEntityId`, `idpSsoServiceUrl`, and `spEntityId` are all provided
   - Verify that certificates are properly base64-encoded

3. **Certificate errors**
   - Ensure certificates are in X.509 format (PEM or DER)
   - Verify certificates are not expired
   - Check that certificate encoding is correct (base64)

4. **Clock skew errors**
   - Increase `clockSkewMinutes` if you're experiencing time synchronization issues
   - Ensure server clocks are synchronized (use NTP)

### Step 6: Managing SAML2 Settings

#### Get SAML2 Settings

```
GET /saml2/settings/{tenantId}
```

Returns the current SAML2 settings (without sensitive data like private keys).

#### Update SAML2 Settings

```
POST /saml2/settings/{tenantId}
```

Update any SAML2 settings. Provide only the fields you want to update.

#### Delete SAML2 Settings

```
DELETE /saml2/settings/{tenantId}
```

Removes all SAML2 settings for a tenant.

### Security Best Practices

1. **Use CA-signed certificates in production**: Self-signed certificates are acceptable for testing but should be replaced with CA-signed certificates for production

2. **Enable signed requests and responses**: Set `signAuthnRequest` and `requireSignedResponse` to `true` for enhanced security

3. **Use HTTPS**: Always use HTTPS for SAML2 endpoints to protect SAML messages in transit

4. **Rotate certificates periodically**: Plan for certificate rotation and update tenant settings accordingly

5. **Monitor SAML2 authentication**: Log and monitor SAML2 authentication attempts for security auditing

6. **Validate SP certificates**: Always validate Service Provider certificates to prevent man-in-the-middle attacks

7. **Use encrypted assertions for sensitive data**: Enable `requireEncryptedAssertion` if handling sensitive user information

### Example: Complete SAML2 Setup Workflow

```bash
# 1. Generate IdP certificate
openssl req -x509 -newkey rsa:2048 -keyout idp_key.pem -out idp_cert.pem -days 365 -nodes \
  -subj "/CN=your-app.com/O=Your Organization"

# 2. Convert certificate to base64
IDP_CERT=$(cat idp_cert.pem | base64 | tr -d '\n')

# 3. Get SP information from Service Provider
SP_ENTITY_ID="https://sp-application.com"
SP_ACS_URL="https://sp-application.com/saml/acs"
SP_CERT="<base64-encoded-sp-certificate>"

# 4. Configure SAML2 settings
curl -X POST "https://your-app.com/saml2/settings/YOUR_TENANT_ID" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"isEnabled\": true,
    \"idpEntityId\": \"https://your-app.com/saml2\",
    \"idpSsoServiceUrl\": \"https://your-app.com/saml2/sso/YOUR_TENANT_ID\",
    \"idpCertificate\": \"$IDP_CERT\",
    \"spEntityId\": \"$SP_ENTITY_ID\",
    \"spAcsUrl\": \"$SP_ACS_URL\",
    \"spCertificate\": \"$SP_CERT\",
    \"nameIdFormat\": \"urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress\",
    \"signAuthnRequest\": true,
    \"requireSignedResponse\": true
  }"

# 5. Test SSO initiation
curl -L "https://your-app.com/saml2/sso/YOUR_TENANT_ID?returnUrl=https://sp-application.com/callback"
```

### Additional Resources

- [SAML 2.0 Technical Overview](http://docs.oasis-open.org/security/saml/Post2.0/sstc-saml-tech-overview-2.0.html)
- [Sustainsys.Saml2 Documentation](https://github.com/Sustainsys/Saml2)
- [SAML2 Metadata Specification](http://docs.oasis-open.org/security/saml/v2.0/saml-metadata-2.0-os.pdf)

## Multi-Factor Authentication (MFA)

This solution supports Time-based One-Time Password (TOTP) multi-factor authentication, allowing users to add an extra layer of security to their accounts using authenticator apps like Google Authenticator, Microsoft Authenticator, or Authy.

### Overview

MFA adds a second authentication factor after password verification. When MFA is enabled for a user:

1. User authenticates with username/password
2. System returns an MFA challenge token (instead of JWT)
3. User provides TOTP code from authenticator app
4. System validates code and returns JWT access token + refresh token

### Features

- **TOTP-based (RFC 6238 compliant)**: Industry-standard time-based one-time passwords
- **Works with standard authenticator apps**: Google Authenticator, Microsoft Authenticator, Authy, 1Password, etc.
- **Backup codes**: Single-use recovery codes for account recovery
- **Per-user enable/disable**: Users can enable or disable MFA for their accounts
- **Tenant-aware**: Works seamlessly with multi-tenant architecture
- **No external dependencies**: Uses ASP.NET Core Identity's built-in TOTP support

### MFA Setup Flow

#### Step 1: Generate MFA Setup Data

**Endpoint:** `POST /api/mfa/setup`

**Authentication:** Requires valid JWT token

**Response:**
```json
{
  "qrCodeUri": "otpauth://totp/BoilerPlate:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=BoilerPlate",
  "manualEntryKey": "JBSW Y3DP EHPK 3PXP",
  "account": "user@example.com",
  "issuer": "BoilerPlate"
}
```

**Example:**
```bash
curl -X POST "https://your-app.com/api/mfa/setup" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json"
```

**What to do:**
1. Scan the QR code with your authenticator app, OR
2. Manually enter the key into your authenticator app
3. Save the backup codes (generated after enabling MFA)

#### Step 2: Verify and Enable MFA

**Endpoint:** `POST /api/mfa/enable`

**Authentication:** Requires valid JWT token

**Request:**
```json
{
  "code": "123456"
}
```

**Example:**
```bash
curl -X POST "https://your-app.com/api/mfa/enable" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "123456"
  }'
```

**Response:**
```json
{
  "message": "MFA enabled successfully"
}
```

**What to do:**
1. Enter the 6-digit code from your authenticator app
2. If valid, MFA is enabled for your account

### Login Flow with MFA

When a user with MFA enabled logs in:

#### Step 1: Authenticate with Username/Password

**Endpoint:** `POST /oauth/token`

**Request:**
```json
{
  "grant_type": "password",
  "username": "user@example.com",
  "password": "password123",
  "tenant_id": "00000000-0000-0000-0000-000000000000"
}
```

**Response (if MFA is enabled):**
```json
{
  "error": "mfa_required",
  "error_description": "Multi-factor authentication is required",
  "mfa_challenge_token": "base64-encoded-challenge-token",
  "mfa_verification_url": "/api/mfa/verify"
}
```

**Note:** The challenge token is valid for 10 minutes and can only be used once.

#### Step 2: Verify MFA Code

**Endpoint:** `POST /api/mfa/verify`

**Authentication:** Not required (challenge token is used instead)

**Request:**
```json
{
  "challengeToken": "base64-encoded-challenge-token",
  "code": "123456"
}
```

**Example:**
```bash
curl -X POST "https://your-app.com/api/mfa/verify" \
  -H "Content-Type: application/json" \
  -d '{
    "challengeToken": "base64-encoded-challenge-token",
    "code": "123456"
  }'
```

**Response:**
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "refresh-token-abc123..."
}
```

#### Alternative: Verify Backup Code

If you've lost access to your authenticator app, you can use a backup code:

**Endpoint:** `POST /api/mfa/verify-backup-code`

**Request:**
```json
{
  "challengeToken": "base64-encoded-challenge-token",
  "backupCode": "ABCD-1234-EFGH-5678"
}
```

**Response:** Same as MFA verify endpoint (JWT access token + refresh token)

**Note:** Backup codes are single-use. After using a backup code, generate new ones.

### Managing MFA

#### Get MFA Status

**Endpoint:** `GET /api/mfa/status`

**Authentication:** Requires valid JWT token

**Response:**
```json
{
  "isEnabled": true,
  "isRequired": false,
  "remainingBackupCodes": 8
}
```

#### Generate Backup Codes

**Endpoint:** `POST /api/mfa/backup-codes`

**Authentication:** Requires valid JWT token

**Response:**
```json
{
  "backupCodes": [
    "ABCD-1234-EFGH-5678",
    "IJKL-9012-MNOP-3456",
    "..."
  ]
}
```

**Note:** Generating new backup codes invalidates all previous backup codes. Save them securely!

#### Disable MFA

**Endpoint:** `POST /api/mfa/disable`

**Authentication:** Requires valid JWT token

**Response:**
```json
{
  "message": "MFA disabled successfully"
}
```

### Complete Example: Enabling and Using MFA

```bash
# 1. Get JWT token (normal login)
TOKEN=$(curl -X POST "https://your-app.com/oauth/token" \
  -H "Content-Type: application/json" \
  -d '{
    "grant_type": "password",
    "username": "user@example.com",
    "password": "password123"
  }' | jq -r '.access_token')

# 2. Generate MFA setup
SETUP=$(curl -X POST "https://your-app.com/api/mfa/setup" \
  -H "Authorization: Bearer $TOKEN")

# 3. Scan QR code or enter manual key in authenticator app
# (Get code from authenticator app: 123456)

# 4. Enable MFA
curl -X POST "https://your-app.com/api/mfa/enable" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"code": "123456"}'

# 5. Generate backup codes
BACKUP_CODES=$(curl -X POST "https://your-app.com/api/mfa/backup-codes" \
  -H "Authorization: Bearer $TOKEN")
# Save these codes securely!

# 6. Login with MFA
LOGIN_RESPONSE=$(curl -X POST "https://your-app.com/oauth/token" \
  -H "Content-Type: application/json" \
  -d '{
    "grant_type": "password",
    "username": "user@example.com",
    "password": "password123"
  }')

# Extract challenge token
CHALLENGE_TOKEN=$(echo $LOGIN_RESPONSE | jq -r '.mfa_challenge_token')

# 7. Verify MFA code
FINAL_TOKEN=$(curl -X POST "https://your-app.com/api/mfa/verify" \
  -H "Content-Type: application/json" \
  -d "{
    \"challengeToken\": \"$CHALLENGE_TOKEN\",
    \"code\": \"123456\"
  }")
```

### Supported Authenticator Apps

MFA works with any TOTP-compatible authenticator app:

- **Google Authenticator** (iOS, Android)
- **Microsoft Authenticator** (iOS, Android)
- **Authy** (iOS, Android, Desktop)
- **1Password** (iOS, Android, Desktop)
- **LastPass Authenticator** (iOS, Android)
- **Duo Mobile** (iOS, Android)
- Any other TOTP-compatible app

### Security Best Practices

1. **Enable MFA for all users**: Encourage or require MFA for enhanced security
2. **Store backup codes securely**: Backup codes should be stored in a password manager or secure location
3. **Rotate backup codes periodically**: Generate new backup codes if you suspect they've been compromised
4. **Use authenticator apps, not SMS**: TOTP is more secure than SMS-based MFA
5. **Monitor MFA status**: Regularly check MFA status and remaining backup codes
6. **Disable MFA if device is lost**: If you lose access to your authenticator app, use a backup code to log in, then disable and re-enable MFA

### Tenant-Level MFA Requirements

Tenants can require MFA for all users by setting a tenant setting:

**Setting Key:** `Mfa.Required`

**Setting Value:** `true` or `false`

**Example:**
```bash
# Set MFA as required for a tenant
curl -X POST "https://your-app.com/api/TenantSettings" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "123e4567-e89b-12d3-a456-426614174000",
    "key": "Mfa.Required",
    "value": "true"
  }'
```

When `Mfa.Required` is set to `true` for a tenant, users will be prompted to enable MFA during their next login.

### Troubleshooting

#### "Invalid MFA code"

- **Clock synchronization**: Ensure your device's clock is synchronized. TOTP codes are time-sensitive.
- **Code expired**: TOTP codes change every 30 seconds. Enter the current code from your authenticator app.
- **Wrong account**: Ensure you're using the correct authenticator entry for this account.

#### "Challenge token expired"

- Challenge tokens expire after 10 minutes. Request a new login to get a new challenge token.

#### "Challenge token already used"

- Challenge tokens are single-use. If you've already used the token, request a new login.

#### "No backup codes remaining"

- Generate new backup codes via `POST /api/mfa/backup-codes`
- If you've lost access to your authenticator app and have no backup codes, contact your administrator

#### "MFA is not enabled"

- Enable MFA first via `POST /api/mfa/setup` and `POST /api/mfa/enable`
- Check MFA status via `GET /api/mfa/status`

### API Reference

| Endpoint | Method | Auth Required | Description |
|----------|--------|---------------|-------------|
| `/api/mfa/status` | GET | Yes | Get MFA status for current user |
| `/api/mfa/setup` | POST | Yes | Generate QR code and setup key |
| `/api/mfa/enable` | POST | Yes | Enable MFA after verification |
| `/api/mfa/disable` | POST | Yes | Disable MFA for current user |
| `/api/mfa/verify` | POST | No | Verify TOTP code and get JWT token |
| `/api/mfa/verify-backup-code` | POST | No | Verify backup code and get JWT token |
| `/api/mfa/backup-codes` | POST | Yes | Generate new backup codes |

## Swagger Documentation

When running in Development mode, Swagger UI is available at `/swagger` with full API documentation and the ability to test endpoints with JWT authentication.
