# PowerShell setup script for BoilerPlate Authentication
# This script can be run directly on Windows if you don't have 'make' installed
# Usage: .\setup.ps1

param(
    [switch]$SkipKeys = $false,
    [switch]$RegenKeys = $false
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-ColorOutput($ForegroundColor, $Message) {
    Write-Host $Message -ForegroundColor $ForegroundColor
}

Write-ColorOutput Cyan "============================================"
Write-ColorOutput Cyan "  BoilerPlate Authentication Setup"
Write-ColorOutput Cyan "============================================"
Write-Host ""

# Verify prerequisites
Write-ColorOutput Yellow "Verifying prerequisites..."

# Check .NET (for ML-DSA key generation)
try {
    $dotnetVersion = & dotnet --version 2>&1
    if ($LASTEXITCODE -ne 0) { throw }
    Write-ColorOutput Green "  ✓ .NET is installed: $dotnetVersion"
} catch {
    Write-ColorOutput Red "  ✗ .NET SDK is not installed"
    Write-ColorOutput Yellow "    Install from: https://dotnet.microsoft.com/download"
    exit 1
}

# Check Docker
try {
    $dockerVersion = & docker --version 2>&1
    if ($LASTEXITCODE -ne 0) { throw }
    Write-ColorOutput Green "  ✓ Docker is installed: $dockerVersion"
} catch {
    Write-ColorOutput Red "  ✗ Docker is not installed"
    Write-ColorOutput Yellow "    Please install Docker Desktop from: https://www.docker.com/products/docker-desktop"
    exit 1
}

# Check Docker Compose
try {
    $composeVersion = & docker compose version 2>&1
    if ($LASTEXITCODE -ne 0) {
        # Try legacy docker-compose
        $composeVersion = & docker-compose --version 2>&1
        if ($LASTEXITCODE -ne 0) { throw }
    }
    Write-ColorOutput Green "  ✓ Docker Compose is installed: $composeVersion"
} catch {
    Write-ColorOutput Red "  ✗ Docker Compose is not installed"
    Write-ColorOutput Yellow "    Docker Compose should be included with Docker Desktop"
    exit 1
}

Write-Host ""

# Setup ML-DSA JWT keys
if (-Not $SkipKeys) {
    Write-ColorOutput Yellow "Setting up ML-DSA JWT keys..."
    
    # Create jwt-keys directory if it doesn't exist
    if (-Not (Test-Path "jwt-keys")) {
        New-Item -ItemType Directory -Path "jwt-keys" | Out-Null
        Write-ColorOutput Green "  ✓ Created jwt-keys directory"
    }
    
    # Check if keys exist and regenerate if requested
    if ($RegenKeys -and (Test-Path "jwt-keys\mldsa_jwk.json")) {
        Write-ColorOutput Yellow "  Regenerating keys (backing up existing)..."
        if (Test-Path "jwt-keys\mldsa_jwk.json") { Move-Item "jwt-keys\mldsa_jwk.json" "jwt-keys\mldsa_jwk.json.bak" -Force }
        if (Test-Path "jwt-keys\mldsa_jwk_base64.txt") { Move-Item "jwt-keys\mldsa_jwk_base64.txt" "jwt-keys\mldsa_jwk_base64.txt.bak" -Force }
    }
    
    # Generate ML-DSA keys if they don't exist
    if (-Not (Test-Path "jwt-keys\mldsa_jwk.json")) {
        Write-ColorOutput Yellow "  Generating ML-DSA key pair..."
        $jwtKeysPath = (Get-Item "jwt-keys").FullName
        & dotnet run --project BoilerPlate.Authentication.WebApi/BoilerPlate.Authentication.WebApi.csproj -- --generate-mldsa-keys $jwtKeysPath
        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput Red "  ✗ Failed to generate ML-DSA keys"
            exit 1
        }
        Write-ColorOutput Green "  ✓ ML-DSA keys generated"
    } else {
        Write-ColorOutput Green "  ✓ ML-DSA keys already exist"
    }
}

Write-Host ""

# Create .env file
Write-ColorOutput Yellow "Creating .env file..."

try {
    if (-Not (Test-Path "jwt-keys\mldsa_jwk_base64.txt")) {
        Write-ColorOutput Red "  ✗ ML-DSA JWT key not found"
        Write-ColorOutput Yellow "    Run with -SkipKeys:`$false to generate keys first"
        exit 1
    }
    
    $mldsaJwkBase64 = (Get-Content "jwt-keys\mldsa_jwk_base64.txt" -Raw).Trim()
    
    # Create .env file content with all configuration
    $envContent = @"
JWT_MLDSA_JWK=$mldsaJwkBase64
JWT_EXPIRATION_MINUTES=60

# Database Connection Strings
ConnectionStrings__PostgreSqlConnection=Host=postgres;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!

# Service Bus Connection Strings
RABBITMQ_CONNECTION_STRING=amqp://admin:SecurePassword123!@rabbitmq:5672/

# MongoDB Connection String
MONGODB_CONNECTION_STRING=mongodb://admin:SecurePassword123!@mongodb:27017/logs?authSource=admin

# Admin User Configuration
ADMIN_USERNAME=admin
ADMIN_PASSWORD=AdminPassword123!
"@
    
    $envContent | Out-File -FilePath ".env" -Encoding utf8 -NoNewline
    
    Write-ColorOutput Green "  ✓ .env file created with all connection strings"
    
    # Save base64 versions for reference
    $privateKeyBase64 | Out-File -FilePath "jwt-keys\private_key_base64.txt" -Encoding utf8 -NoNewline
    $publicKeyBase64 | Out-File -FilePath "jwt-keys\public_key_base64.txt" -Encoding utf8 -NoNewline
    
} catch {
    Write-ColorOutput Red "  ✗ Failed to create .env file"
    Write-ColorOutput Red "    Error: $($_.Exception.Message)"
    exit 1
}

Write-Host ""
Write-ColorOutput Green "============================================"
Write-ColorOutput Green "  ✓ Setup Complete!"
Write-ColorOutput Green "============================================"
Write-Host ""
Write-ColorOutput Yellow "Next steps:"
Write-Host "  1. Review the .env file (contains base64-encoded JWT keys)"
Write-Host "  2. Start services: docker-compose up -d"
Write-Host "  3. Access Swagger UI: http://localhost:8080/swagger"
Write-Host ""
Write-ColorOutput Cyan "To start services, run:"
Write-ColorOutput White "  docker-compose up -d"
Write-Host ""
