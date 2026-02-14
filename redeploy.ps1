# PowerShell redeploy script for BoilerPlate
# Equivalent to: make redeploy
# Usage: .\redeploy.ps1
# Run from the project root (where docker-compose.yml is located)

$ErrorActionPreference = "Stop"

function Write-ColorOutput($ForegroundColor, $Message) {
    Write-Host $Message -ForegroundColor $ForegroundColor
}

Write-ColorOutput Cyan "============================================"
Write-ColorOutput Cyan "  BoilerPlate Redeploy"
Write-ColorOutput Cyan "============================================"
Write-Host ""

$projectRoot = $PSScriptRoot
if (-not $projectRoot) { $projectRoot = Get-Location }

# 1. Build .NET projects
Write-ColorOutput Yellow "Building webapi .NET project..."
Push-Location $projectRoot
try {
    dotnet build BoilerPlate.Authentication.WebApi/BoilerPlate.Authentication.WebApi.csproj -c Release -m
    if ($LASTEXITCODE -ne 0) { throw "WebAPI build failed" }
    Write-ColorOutput Green "  ✓ WebAPI project built successfully"
} finally { Pop-Location }

Write-ColorOutput Yellow "Building audit service .NET project..."
Push-Location $projectRoot
try {
    dotnet build BoilerPlate.Services.Audit/BoilerPlate.Services.Audit.csproj -c Release -m
    if ($LASTEXITCODE -ne 0) { throw "Audit build failed" }
    Write-ColorOutput Green "  ✓ Audit service project built successfully"
} finally { Pop-Location }

Write-ColorOutput Yellow "Building event logs service .NET project..."
Push-Location $projectRoot
try {
    dotnet build BoilerPlate.Services.EventLogs/BoilerPlate.Services.EventLogs.csproj -c Release -m
    if ($LASTEXITCODE -ne 0) { throw "Event logs build failed" }
    Write-ColorOutput Green "  ✓ Event logs service project built successfully"
} finally { Pop-Location }

Write-ColorOutput Yellow "Building diagnostics API .NET project..."
Push-Location $projectRoot
try {
    dotnet build BoilerPlate.Diagnostics.WebApi/BoilerPlate.Diagnostics.WebApi.csproj -c Release -m
    if ($LASTEXITCODE -ne 0) { throw "Diagnostics build failed" }
    Write-ColorOutput Green "  ✓ Diagnostics API project built successfully"
} finally { Pop-Location }

# 2. Build frontend (optional - Docker can build if this fails)
Write-ColorOutput Yellow "Building frontend Angular project..."
Push-Location $projectRoot
try {
    if (Get-Command node -ErrorAction SilentlyContinue) {
        Set-Location BoilerPlate.Frontend
        if (-not (Test-Path node_modules)) { npm install }
        $env:NODE_OPTIONS = "--max-old-space-size=4096"
        npm run build
        if ($LASTEXITCODE -ne 0) { Write-ColorOutput Yellow "  ⚠ Frontend build failed. Docker build will handle it." }
        else { Write-ColorOutput Green "  ✓ Frontend project built successfully" }
    } else {
        Write-ColorOutput Yellow "  ⚠ Node.js not installed. Docker build will handle frontend."
    }
} finally { Pop-Location }

# 3. Ensure PostgreSQL is running (for migrations)
Write-ColorOutput Yellow "Ensuring PostgreSQL is running..."
Push-Location $projectRoot
try {
    $postgresRunning = docker ps --filter "name=postgres-auth" --format "{{.Names}}" 2>$null | Select-String "postgres-auth"
    if (-not $postgresRunning) {
        if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
            docker-compose up -d postgres 2>&1 | Out-Null
        } else {
            docker compose up -d postgres 2>&1 | Out-Null
        }
        Start-Sleep -Seconds 5
    }
} finally { Pop-Location }

# 4. Migrations
Write-ColorOutput Yellow "Applying database migrations..."
if (-not (Test-Path "$projectRoot\.env")) {
    Write-ColorOutput Red "  ✗ .env file not found. Run .\setup.ps1 or make setup-env first."
    exit 1
}
Push-Location $projectRoot
try {
    $dotnetEf = & dotnet tool list -g 2>$null | Select-String "dotnet-ef"
    if (-not $dotnetEf) {
        Write-ColorOutput Yellow "  Installing dotnet-ef tool..."
        dotnet tool install --global dotnet-ef --version 8.0.0
    }
    $env:Path = "$env:USERPROFILE\.dotnet\tools;$env:Path"
    $env:PGPASSWORD = "SecurePassword123!"
    $env:ConnectionStrings__PostgreSqlConnection = "Host=localhost;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!"
    dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext 2>&1 | Where-Object { $_ -notmatch "MONGODB_CONNECTION_STRING" }
    if ($LASTEXITCODE -eq 0) { Write-ColorOutput Green "  ✓ Migrations applied successfully" }
    else { Write-ColorOutput Yellow "  ⚠ Migration failed. Ensure PostgreSQL is running. Run 'make migrate' manually." }
} finally { Pop-Location }

# 5. Rebuild Docker images
Write-ColorOutput Yellow "Rebuilding Docker images..."
Push-Location $projectRoot
try {
    $env:DOCKER_BUILDKIT = "1"
    @(
        @{ Name = "webapi"; File = "BoilerPlate.Authentication.WebApi/Dockerfile"; Tag = "boilerplate-authentication-webapi:latest" },
        @{ Name = "audit"; File = "BoilerPlate.Services.Audit/Dockerfile"; Tag = "boilerplate-services-audit:latest" },
        @{ Name = "event-logs"; File = "BoilerPlate.Services.EventLogs/Dockerfile"; Tag = "boilerplate-services-event-logs:latest" },
        @{ Name = "diagnostics"; File = "BoilerPlate.Diagnostics.WebApi/Dockerfile"; Tag = "boilerplate-diagnostics-webapi:latest" },
        @{ Name = "frontend"; File = "BoilerPlate.Frontend/Dockerfile"; Tag = "boilerplate-frontend:latest" }
    ) | ForEach-Object {
        Write-ColorOutput Yellow "  Building $($_.Name)..."
        docker build --pull=false -f $_.File -t $_.Tag .
        if ($LASTEXITCODE -ne 0) { throw "$($_.Name) image build failed" }
        Write-ColorOutput Green "  ✓ $($_.Name) image built successfully"
    }
} finally { Pop-Location }

# 6. Start services
Write-ColorOutput Yellow "Starting Docker services..."
Push-Location $projectRoot
try {
    if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
        docker-compose up -d webapi audit event-logs diagnostics frontend
    } else {
        docker compose up -d webapi audit event-logs diagnostics frontend
    }
    if ($LASTEXITCODE -ne 0) { throw "Failed to start services" }
    Write-ColorOutput Green "  ✓ Services started"
} finally { Pop-Location }

Write-Host ""
Write-ColorOutput Green "============================================"
Write-ColorOutput Green "  ✓ Redeploy complete!"
Write-ColorOutput Green "============================================"
Write-Host ""
Write-ColorOutput Yellow "Service URLs (all via port 4200):"
Write-Host "  - Frontend: https://localhost:4200"
Write-Host "  - Auth API Swagger: https://localhost:4200/swagger"
Write-Host "  - Diagnostics Swagger: https://localhost:4200/diagnostics/swagger"
Write-Host "  - RabbitMQ Management: https://localhost:4200/amqp/"
Write-Host ""
