# Authentication Web API

OAuth2 authentication API with JWT tokens using RS256 (RSA asymmetric encryption) for PQC-resistant signatures.

## Features

- OAuth2 Resource Owner Password Credentials grant
- JWT token generation with RS256 (RSA-2048) asymmetric encryption
- Multi-tenant support
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

## Swagger Documentation

When running in Development mode, Swagger UI is available at `/swagger` with full API documentation and the ability to test endpoints with JWT authentication.
