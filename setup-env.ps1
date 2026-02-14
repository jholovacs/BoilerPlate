# PowerShell script to create .env file with base64-encoded JWT keys
# This script is called by the Makefile on Windows systems

Write-Host "Creating .env file with base64-encoded JWT keys..." -ForegroundColor Yellow

# Check if JWT keys exist
if (-Not (Test-Path "jwt-keys\private_key.pem")) {
    Write-Host "ERROR: Private key not found at jwt-keys\private_key.pem" -ForegroundColor Red
    Write-Host "Please run 'make setup-keys' first to generate JWT keys" -ForegroundColor Yellow
    exit 1
}

if (-Not (Test-Path "jwt-keys\public_key.pem")) {
    Write-Host "ERROR: Public key not found at jwt-keys\public_key.pem" -ForegroundColor Red
    Write-Host "Please run 'make setup-keys' first to generate JWT keys" -ForegroundColor Yellow
    exit 1
}

try {
    # Read and encode private key as base64
    $privateKeyContent = Get-Content "jwt-keys\private_key.pem" -Raw
    $privateKeyBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($privateKeyContent))
    
    # Read and encode public key as base64
    $publicKeyContent = Get-Content "jwt-keys\public_key.pem" -Raw
    $publicKeyBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($publicKeyContent))
    
    # Create .env file with all configuration
    $envContent = @"
JWT_PRIVATE_KEY=$privateKeyBase64
JWT_PUBLIC_KEY=$publicKeyBase64
JWT_EXPIRATION_MINUTES=60

# JWT Issuer URL for microservices that validate tokens (e.g. Diagnostics).
# Used to fetch public key from /.well-known/jwks.json when JWT_PUBLIC_KEY is not set.
# Docker default: http://webapi:8080 | Local: http://localhost:8080
# JWT_ISSUER_URL=http://webapi:8080

# Database Connection Strings
ConnectionStrings__PostgreSqlConnection=Host=postgres;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!

# Service Bus Connection Strings
RABBITMQ_CONNECTION_STRING=amqp://admin:SecurePassword123!@rabbitmq:5672/

# MongoDB Connection String
MONGODB_CONNECTION_STRING=mongodb://admin:SecurePassword123!@mongodb:27017/logs?authSource=admin

# OpenTelemetry Collector Connection String
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317

# Admin User Configuration
ADMIN_USERNAME=admin
ADMIN_PASSWORD=AdminPassword123!
"@
    
    $envContent | Out-File -FilePath ".env" -Encoding utf8 -NoNewline
    
    Write-Host "✓ .env file created successfully with all connection strings" -ForegroundColor Green
    
    # Optionally save base64 versions for reference
    $privateKeyBase64 | Out-File -FilePath "jwt-keys\private_key_base64.txt" -Encoding utf8 -NoNewline
    $publicKeyBase64 | Out-File -FilePath "jwt-keys\public_key_base64.txt" -Encoding utf8 -NoNewline
    
} catch {
    Write-Host "ERROR: Failed to create .env file" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
