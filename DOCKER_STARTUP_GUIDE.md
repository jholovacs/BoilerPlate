# Docker Desktop Startup Guide

This guide will walk you through setting up and running the BoilerPlate Authentication Web API and all its dependencies in Docker Desktop.

## Quick Start Summary

The **recommended** approach is to use the automated Makefile or PowerShell scripts, which handle all setup steps automatically.

### Using Makefile (Recommended for macOS/Linux/WSL/Git Bash)

The `make setup` command handles everything automatically:

```bash
make setup          # Complete automated setup:
                     # - Verifies prerequisites (OpenSSL, Docker, .NET SDK, dotnet-ef)
                     # - Generates JWT keys (if not exist)
                     # - Creates .env file with base64-encoded keys and connection strings
                     # - Ensures all services are running (PostgreSQL, RabbitMQ, MongoDB)
                     # - Builds the .NET WebAPI project
                     # - Runs database migrations
                     # - Rebuilds the webapi Docker image (using cached base images)
                     # - Starts the webapi container
                     # 
                     # After completion, all services are running and ready!
```

**Access the API:** http://localhost:8080/swagger

**Note:** See [SETUP.md](SETUP.md) for detailed documentation of all available Makefile commands.

### Using PowerShell Script (Windows)

```powershell
.\setup.ps1         # Automated setup (same as make setup but for Windows PowerShell)
                    # After completion, start services:
docker-compose up -d
```

### Manual Setup (For Understanding the Process)

If you prefer to understand each step or need to customize the setup:

1. **Install Docker Desktop** (if not already installed)
2. **Generate JWT keys** using OpenSSL and **encode as base64** (see "Generate JWT Keys" section)
3. **Create `.env` file** with base64-encoded JWT keys and connection strings (see "Configure and Run Web API" section)
4. **Ensure services are running** (PostgreSQL, RabbitMQ, MongoDB)
5. **Build the .NET project** and run migrations
6. **Build Docker image** and start containers
7. **Access the API** at http://localhost:8080/swagger

**For automated setup, see [SETUP.md](SETUP.md). For detailed manual instructions, continue reading below.**

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Install Docker Desktop](#install-docker-desktop)
3. [Setup Services](#setup-services)
   - [PostgreSQL Database](#postgresql-database)
   - [RabbitMQ Message Broker](#rabbitmq-message-broker)
   - [MongoDB Logging Database](#mongodb-logging-database)
4. [Generate JWT Keys](#generate-jwt-keys)
5. [Configure and Run Web API](#configure-and-run-web-api)
6. [Verify Setup](#verify-setup)
7. [Accessing Services](#accessing-services)
8. [Troubleshooting](#troubleshooting)

## Prerequisites

- Windows 10/11 (64-bit) or macOS or Linux
- At least 4GB of free RAM
- Administrator/sudo access for installation
- OpenSSL installed (for generating JWT keys)

### Installing OpenSSL

**Windows:**
- Download OpenSSL from: https://slproweb.com/products/Win32OpenSSL.html
- Or use Git Bash (includes OpenSSL)
- Or use Chocolatey: `choco install openssl`

**macOS:**
```bash
brew install openssl
```

**Linux (Ubuntu/Debian):**
```bash
sudo apt-get update
sudo apt-get install openssl
```

## Install Docker Desktop

### Windows

1. Download Docker Desktop for Windows from: https://www.docker.com/products/docker-desktop
2. Run the installer and follow the setup wizard
3. Restart your computer if prompted
4. Launch Docker Desktop and wait for it to start (whale icon in system tray)
5. Verify installation:
   ```powershell
   docker --version
   docker-compose --version
   ```

### macOS

1. Download Docker Desktop for Mac from: https://www.docker.com/products/docker-desktop
2. Open the `.dmg` file and drag Docker to Applications
3. Launch Docker Desktop from Applications
4. Verify installation:
   ```bash
   docker --version
   docker-compose --version
   ```

### Linux (Ubuntu/Debian)

```bash
# Remove old versions
sudo apt-get remove docker docker-engine docker.io containerd runc

# Install prerequisites
sudo apt-get update
sudo apt-get install ca-certificates curl gnupg lsb-release

# Add Docker's official GPG key
sudo mkdir -p /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg

# Set up the repository
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Install Docker
sudo apt-get update
sudo apt-get install docker-ce docker-ce-cli containerd.io docker-compose-plugin

# Add your user to docker group (to run without sudo)
sudo usermod -aG docker $USER

# Log out and log back in for group changes to take effect
```

## Setup Services

We'll run all services using Docker containers. You can run them individually or use the provided `docker-compose.yml` file.

**Note:** If you're using `make setup`, the services will be automatically started for you. This section is for manual setup or understanding the process.

### PostgreSQL Database

PostgreSQL will be used as the authentication database.

#### Option 1: Using Docker Run Command

```bash
docker run -d \
  --name postgres-auth \
  --restart unless-stopped \
  -e POSTGRES_USER=boilerplate \
  -e POSTGRES_PASSWORD=SecurePassword123! \
  -e POSTGRES_DB=BoilerPlateAuth \
  -p 5432:5432 \
  postgres:15-alpine
```

#### Option 2: Using Docker Compose (Recommended)

A `docker-compose.yml` file is already provided in the solution root. You can use it as-is or customize it. The file includes configurations for all required services.

**Using the provided docker-compose.yml:**

```bash
# Start all services (PostgreSQL, RabbitMQ, MongoDB) without the Web API
docker-compose up -d postgres rabbitmq mongodb

# Or using Makefile (automatically handles dependencies and health checks):
make ensure-services
```

Or to start all services including the Web API:
```bash
docker-compose up -d

# Or using Makefile:
make docker-up
```

**Note:** The Web API service in docker-compose requires:
1. JWT keys to be set via environment variables (base64-encoded in `.env` file)
2. Database migrations to be run first
3. The .NET project to be built

**Recommended:** Use `make setup` which handles all of this automatically. See [SETUP.md](SETUP.md) for details.

#### Verify PostgreSQL is Running

```bash
docker ps | grep postgres-auth
```

You should see the container running. Test the connection:
```bash
docker exec -it postgres-auth psql -U boilerplate -d BoilerPlateAuth -c "SELECT version();"
```

**Connection String Format:**
```
Host=localhost;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!
```

### RabbitMQ Message Broker

RabbitMQ with the management dashboard will be used for asynchronous messaging.

#### Option 1: Using Docker Run Command

```bash
docker run -d \
  --name rabbitmq-auth \
  --restart unless-stopped \
  -e RABBITMQ_DEFAULT_USER=admin \
  -e RABBITMQ_DEFAULT_PASS=SecurePassword123! \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3-management-alpine
```

#### Option 2: Using Docker Compose

Use the `docker-compose.yml` file shown above in the PostgreSQL section.

#### Verify RabbitMQ is Running

```bash
docker ps | grep rabbitmq-auth
```

**Access the Management Dashboard:**
- URL: http://localhost:15672
- Username: `admin`
- Password: `SecurePassword123!`

**Connection String Format:**
```
amqp://admin:SecurePassword123!@localhost:5672/
```

### MongoDB Logging Database

MongoDB will be used to store application logs via Serilog.

#### Option 1: Using Docker Run Command

```bash
docker run -d \
  --name mongodb-logs \
  --restart unless-stopped \
  -e MONGO_INITDB_ROOT_USERNAME=admin \
  -e MONGO_INITDB_ROOT_PASSWORD=SecurePassword123! \
  -p 27017:27017 \
  mongo:7.0
```

#### Option 2: Using Docker Compose

Use the `docker-compose.yml` file shown above in the PostgreSQL section.

#### Verify MongoDB is Running

```bash
docker ps | grep mongodb-logs
```

Test the connection:
```bash
docker exec -it mongodb-logs mongosh -u admin -p SecurePassword123! --authenticationDatabase admin --eval "db.adminCommand('ping')"
```

**Connection String Format:**
```
mongodb://admin:SecurePassword123!@localhost:27017/logs?authSource=admin
```

## Generate JWT Keys

Before running the Web API, you need to generate RSA key pairs for JWT token signing and validation. These keys are **required** for the application to start.

**Automated Approach:** The `make setup` command automatically generates JWT keys if they don't exist. You can also use:
- `make setup-keys` - Generate JWT keys only
- `make setup-env` - Create .env file with base64-encoded keys

**Manual Approach:** Follow the steps below if you want to generate keys manually or understand the process.

### Generate Keys Using OpenSSL

#### Step 1: Create a directory for keys

```bash
# Windows (PowerShell)
mkdir jwt-keys
cd jwt-keys

# macOS/Linux
mkdir -p jwt-keys
cd jwt-keys
```

#### Step 2: Generate Private Key

```bash
# Generate a 2048-bit RSA private key
openssl genrsa -out private_key.pem 2048
```

#### Step 3: Extract Public Key

```bash
# Extract the public key from the private key
openssl rsa -in private_key.pem -pubout -out public_key.pem
```

#### Step 4: Verify Keys

```bash
# Verify private key
openssl rsa -in private_key.pem -check -noout

# View public key info
openssl rsa -in public_key.pem -pubin -text -noout | head -5
```

#### Step 5: Encode Keys as Base64 for Environment Variables

For Docker environment variables, it's recommended to base64-encode the keys to avoid issues with newlines and special characters. The application will automatically detect and decode base64-encoded keys.

**Windows PowerShell:**
```powershell
# Read private key and encode as base64
$privateKeyContent = Get-Content private_key.pem -Raw
$privateKeyBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($privateKeyContent))
$privateKeyBase64 | Out-File -FilePath private_key_base64.txt -NoNewline

# Read public key and encode as base64
$publicKeyContent = Get-Content public_key.pem -Raw
$publicKeyBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($publicKeyContent))
$publicKeyBase64 | Out-File -FilePath public_key_base64.txt -NoNewline

# Display for copying (optional)
Write-Host "JWT_PRIVATE_KEY (base64):"
Write-Host $privateKeyBase64
Write-Host ""
Write-Host "JWT_PUBLIC_KEY (base64):"
Write-Host $publicKeyBase64
```

**macOS/Linux:**
```bash
# Encode private key as base64
base64 private_key.pem > private_key_base64.txt

# Encode public key as base64
base64 public_key.pem > public_key_base64.txt

# Display for copying (optional)
echo "JWT_PRIVATE_KEY (base64):"
cat private_key_base64.txt
echo ""
echo "JWT_PUBLIC_KEY (base64):"
cat public_key_base64.txt
```

**Alternative: If you prefer to use PEM format directly**, the application also supports plain PEM keys with newlines. However, base64 encoding is more reliable for Docker environment variables.

**Note:** 
- Base64-encoded keys will be automatically detected and decoded by the application
- The application also supports plain PEM format (with `-----BEGIN` markers) if you prefer that approach
- Base64 encoding avoids issues with newlines, quotes, and special characters in environment variables

## Configure and Run Web API

This section covers how to configure and run the Web API in a Docker container.

**Automated Approach:** The `make setup` command automatically:
1. Creates the `.env` file with all required configuration (JWT keys, connection strings, admin credentials)
2. Builds the .NET project in Release configuration
3. Runs database migrations
4. Rebuilds the Docker image using cached base images
5. Starts the webapi container

**Manual Approach:** Follow the steps below if you want to configure and run manually or understand each step.

### Step 1: Verify Configuration

The Web API is already configured to use PostgreSQL, RabbitMQ, and MongoDB. The configuration is read from environment variables, which are provided via the `.env` file when using docker-compose.

**Note:** No code changes are needed. The application is already configured correctly for PostgreSQL, RabbitMQ, and MongoDB.

### Step 2: Create .env File with Configuration

Create a `.env` file in the solution root (for Docker Compose) or set environment variables directly:

**Windows PowerShell:**
```powershell
# Database Connection
$env:ConnectionStrings__PostgreSqlConnection="Host=localhost;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!"

# JWT Configuration (use base64-encoded keys - recommended for Docker)
# Base64 encode the keys first (see "Generate JWT Keys" section)
$privateKeyBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content jwt-keys/private_key.pem -Raw)))
$publicKeyBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content jwt-keys/public_key.pem -Raw)))
$env:JWT_PRIVATE_KEY=$privateKeyBase64
$env:JWT_PUBLIC_KEY=$publicKeyBase64
$env:JWT_EXPIRATION_MINUTES="60"

# MongoDB Logging (REQUIRED)
$env:MONGODB_CONNECTION_STRING="mongodb://admin:SecurePassword123!@localhost:27017/logs?authSource=admin"

# RabbitMQ (optional, will use appsettings.json default if not set)
$env:RABBITMQ_CONNECTION_STRING="amqp://admin:SecurePassword123!@localhost:5672/"

# Admin User Configuration (optional, will use defaults if not set)
$env:ADMIN_USERNAME="admin"
$env:ADMIN_PASSWORD="AdminPassword123!"
```

**macOS/Linux:**
```bash
# Database Connection
export ConnectionStrings__PostgreSqlConnection="Host=localhost;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!"

# JWT Configuration (use base64-encoded keys - recommended for Docker)
# Base64 encode the keys first (see "Generate JWT Keys" section)
export JWT_PRIVATE_KEY="$(cat jwt-keys/private_key.pem | base64 | tr -d '\n')"
export JWT_PUBLIC_KEY="$(cat jwt-keys/public_key.pem | base64 | tr -d '\n')"
export JWT_EXPIRATION_MINUTES="60"

# MongoDB Logging (REQUIRED)
export MONGODB_CONNECTION_STRING="mongodb://admin:SecurePassword123!@localhost:27017/logs?authSource=admin"

# RabbitMQ
export RABBITMQ_CONNECTION_STRING="amqp://admin:SecurePassword123!@localhost:5672/"

# Admin User Configuration
export ADMIN_USERNAME="admin"
export ADMIN_PASSWORD="AdminPassword123!"
```

### Step 3: Build the .NET Project

Before building the Docker image, build the .NET project:

```bash
# Build the project
dotnet build BoilerPlate.Authentication.WebApi/BoilerPlate.Authentication.WebApi.csproj -c Release

# Or using Makefile:
make build-webapi-project
```

### Step 4: Run Database Migrations

Ensure PostgreSQL is running, then apply migrations:

```bash
# Using dotnet ef directly:
ConnectionStrings__PostgreSqlConnection="Host=localhost;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!" dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext

# Or using Makefile (automatically ensures PostgreSQL is ready):
make run-migration
# Or:
make migrate
```

**Note:** 
- The `make setup` command automatically builds the project and runs migrations as part of the setup process
- Migrations will be applied automatically when the application starts if the database is empty, but it's recommended to run them manually for production

### Step 5: Build the Docker Image

From the solution root directory:

```bash
# Build the image (this will pull base images on first run)
docker build -f BoilerPlate.Authentication.WebApi/Dockerfile -t boilerplate-authentication-webapi:latest .

# Or using Makefile (uses cached base images, faster rebuild):
make rebuild-webapi-docker
```

**Note:** 
- Make sure Docker Desktop is running before building
- The build context is the solution root directory (`.`)
- The Dockerfile is in the `BoilerPlate.Authentication.WebApi` directory
- This build will take a few minutes the first time as it downloads .NET images and restores packages
- Subsequent builds using `make rebuild-webapi-docker` will use cached base images and only rebuild code changes

**Troubleshooting Build Issues:**

If you encounter build errors related to project references, ensure all projects are built successfully first:
```bash
dotnet build BoilerPlate.sln
```

### Step 6: Run the Web API Container

**Windows PowerShell:**
```powershell
docker run -d `
  --name boilerplate-auth-api `
  --restart unless-stopped `
  -p 8080:8080 `
  -p 8081:8081 `
  -e ConnectionStrings__PostgreSqlConnection="Host=host.docker.internal;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!" `
  -e JWT_PRIVATE_KEY="$([Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content jwt-keys/private_key.pem -Raw))))" `
  -e JWT_PUBLIC_KEY="$([Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content jwt-keys/public_key.pem -Raw))))" `
  -e JWT_EXPIRATION_MINUTES="60" `
  -e MONGODB_CONNECTION_STRING="mongodb://admin:SecurePassword123!@host.docker.internal:27017/logs?authSource=admin" `
  -e RABBITMQ_CONNECTION_STRING="amqp://admin:SecurePassword123!@host.docker.internal:5672/" `
  -e ADMIN_USERNAME="admin" `
  -e ADMIN_PASSWORD="AdminPassword123!" `
  boilerplate-auth-api:latest
```

**macOS/Linux:**
```bash
docker run -d \
  --name boilerplate-auth-api \
  --restart unless-stopped \
  -p 8080:8080 \
  -p 8081:8081 \
  -e ConnectionStrings__PostgreSqlConnection="Host=host.docker.internal;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!" \
  -e JWT_PRIVATE_KEY="$(cat jwt-keys/private_key.pem | base64 | tr -d '\n')" \
  -e JWT_PUBLIC_KEY="$(cat jwt-keys/public_key.pem | base64 | tr -d '\n')" \
  -e JWT_EXPIRATION_MINUTES="60" \
  -e MONGODB_CONNECTION_STRING="mongodb://admin:SecurePassword123!@host.docker.internal:27017/logs?authSource=admin" \
  -e RABBITMQ_CONNECTION_STRING="amqp://admin:SecurePassword123!@host.docker.internal:5672/" \
  -e ADMIN_USERNAME="admin" \
  -e ADMIN_PASSWORD="AdminPassword123!" \
  boilerplate-auth-api:latest
```

**Important Notes:**
- Use `host.docker.internal` instead of `localhost` when connecting from a Docker container to services on the host machine (Windows/macOS)
- On Linux, you may need to use `172.17.0.1` or the actual host IP address instead of `host.docker.internal`
- Make sure all dependency containers (PostgreSQL, RabbitMQ, MongoDB) are running before starting the API container

**Using Makefile (Recommended):**
```bash
# Ensure services are running and healthy
make ensure-services

# Start the webapi container
make docker-up-webapi
```

### Alternative: Using Docker Compose (Complete Setup)

The `docker-compose.yml` file in the solution root includes all services. The **recommended approach** is to use `make setup`, which automatically handles all of these steps.

For manual setup with docker-compose:

1. **Generate JWT keys** (see "Generate JWT Keys" section above, or use `make setup-keys`)
2. **Create a `.env` file** in the solution root directory with base64-encoded keys and connection strings:

**Automated (Recommended):**
```bash
# Automatically creates .env file with all required settings:
make setup-env
```

**Windows PowerShell - Creating .env file manually:**
```powershell
# Encode keys as base64
$privateKeyBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content jwt-keys/private_key.pem -Raw)))
$publicKeyBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content jwt-keys/public_key.pem -Raw)))

# Create .env file with all required configuration
@"
JWT_PRIVATE_KEY=$privateKeyBase64
JWT_PUBLIC_KEY=$publicKeyBase64
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
"@ | Out-File -FilePath .env -Encoding utf8
```

**macOS/Linux - Creating .env file manually:**
```bash
# Create .env file with base64-encoded keys and all connection strings
cat > .env << EOF
JWT_PRIVATE_KEY=$(cat jwt-keys/private_key.pem | base64 | tr -d '\n')
JWT_PUBLIC_KEY=$(cat jwt-keys/public_key.pem | base64 | tr -d '\n')
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
EOF
```

**Alternative: Using PEM format directly**

If you prefer to use PEM format (with `-----BEGIN` markers) instead of base64:
- The application supports both base64-encoded keys and plain PEM format
- For PEM format, ensure newlines are preserved or use `\n` escape sequences
- Base64 encoding is recommended as it avoids newline and special character issues

Then run everything together:

**Using Makefile (Recommended - handles build, migrations, and startup):**
```bash
# Complete automated setup:
make setup

# Or if you've already run setup once, just rebuild and restart:
make rebuild-webapi-docker
make docker-up-webapi
```

**Using Docker Compose directly:**
```bash
# Build and start all services
docker-compose up -d --build

# View logs
docker-compose logs -f webapi

# Or using Makefile:
make docker-logs-webapi
```

**Note:** 
- In docker-compose, services can communicate using service names (`postgres`, `mongodb`, `rabbitmq`) instead of `host.docker.internal` or `localhost`. The connection strings in the docker-compose.yml file use service names for inter-container communication.
- The `make setup` command automatically ensures all services are running, builds the project, runs migrations, and starts the webapi container. See [SETUP.md](SETUP.md) for more details.

## Verify Setup

### Check All Containers Are Running

```bash
docker ps
```

You should see all four containers:
- `postgres-auth`
- `rabbitmq-auth`
- `mongodb-logs`
- `boilerplate-auth-api`

### Check Container Logs

**Web API Logs:**
```bash
docker logs boilerplate-auth-api
```

**PostgreSQL Logs:**
```bash
docker logs postgres-auth
```

**RabbitMQ Logs:**
```bash
docker logs rabbitmq-auth
```

**MongoDB Logs:**
```bash
docker logs mongodb-logs
```

### Test API Health

```bash
# Windows PowerShell
curl http://localhost:8080/health

# macOS/Linux
curl http://localhost:8080/health
```

Or open in browser: http://localhost:8080/health

### Access Swagger UI

Open in browser: http://localhost:8080/swagger

## Accessing Services

### Frontend

- **URL:** http://localhost:4200 (local dev) or https://localhost:4200 (Docker with nginx)
- **Login:** Use admin credentials configured for the Web API (default: `admin` / `AdminPassword123!`)
- **Role-based access:**
  - **All users:** Account, Change password
  - **Tenant/User/Role Administrators:** My tenant
  - **Service Administrators:** Tenants, RabbitMQ Management (auto-login), Event logs, Audit logs
- **RabbitMQ Management:** Service Administrators get a side-nav link that opens RabbitMQ Management with credentials automatically supplied (no manual login)
- See [BoilerPlate.Frontend/README.md](../BoilerPlate.Frontend/README.md) for full access documentation

### Web API

- **API Base URL:** http://localhost:8080
- **Swagger UI:** http://localhost:8080/swagger
- **Health Check:** http://localhost:8080/health

### PostgreSQL

- **Host:** localhost
- **Port:** 5432
- **Database:** BoilerPlateAuth
- **Username:** boilerplate
- **Password:** SecurePassword123!

**Connect using psql:**
```bash
docker exec -it postgres-auth psql -U boilerplate -d BoilerPlateAuth
```

**Or using a GUI client:**
- pgAdmin: https://www.pgadmin.org/
- DBeaver: https://dbeaver.io/
- Connection string: `postgresql://boilerplate:SecurePassword123!@localhost:5432/BoilerPlateAuth`

### RabbitMQ Management Dashboard

- **Direct URL:** http://localhost:15672
- **Via frontend:** Service Administrators can use the "RabbitMQ Management" link in the side nav for automatic login (no credentials needed)
- **Manual login:** Username `admin`, Password `SecurePassword123!`

### MongoDB

- **Host:** localhost
- **Port:** 27017
- **Username:** admin
- **Password:** SecurePassword123!
- **Authentication Database:** admin

**Connect using mongosh:**
```bash
docker exec -it mongodb-logs mongosh -u admin -p SecurePassword123! --authenticationDatabase admin
```

**View logs collection:**
```javascript
use logs
db.logs.find().sort({ Timestamp: -1 }).limit(10).pretty()
```

**Or using a GUI client:**
- MongoDB Compass: https://www.mongodb.com/products/compass
- Connection string: `mongodb://admin:SecurePassword123!@localhost:27017/logs?authSource=admin`

## Troubleshooting

### Container Won't Start

1. **Check Docker Desktop is running**
   - Look for the Docker whale icon in system tray (Windows/macOS)
   - Ensure Docker daemon is running: `docker ps`

2. **Check port conflicts**
   ```bash
   # Windows
   netstat -ano | findstr :8080
   netstat -ano | findstr :5432
   netstat -ano | findstr :5672
   netstat -ano | findstr :27017
   
   # macOS/Linux
   lsof -i :8080
   lsof -i :5432
   lsof -i :5672
   lsof -i :27017
   ```

3. **Check container logs**
   ```bash
   docker logs <container-name>
   ```

### Web API Can't Connect to Services

1. **On Windows/macOS:** Use `host.docker.internal` instead of `localhost` in connection strings when running containers
2. **On Linux:** Use the host's IP address or `172.17.0.1` instead of `host.docker.internal`
3. **Check services are running:**
   ```bash
   docker ps
   ```
4. **Verify health checks:**
   ```bash
   docker inspect <container-name> | grep -A 10 Health
   ```

### PostgreSQL Connection Issues

1. **Verify PostgreSQL is accepting connections:**
   ```bash
   docker exec -it postgres-auth psql -U boilerplate -d BoilerPlateAuth -c "SELECT 1;"
   ```

2. **Check PostgreSQL logs:**
   ```bash
   docker logs postgres-auth
   ```

3. **Verify connection string format:**
   ```
   Host=host.docker.internal;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!
   ```

### RabbitMQ Connection Issues

1. **Verify RabbitMQ is running:**
   ```bash
   docker exec -it rabbitmq-auth rabbitmq-diagnostics ping
   ```

2. **Check management UI is accessible:**
   - Open http://localhost:15672 in browser
   - Login with admin/SecurePassword123!

3. **Verify connection string:**
   ```
   amqp://admin:SecurePassword123!@host.docker.internal:5672/
   ```

### MongoDB Connection Issues

1. **Verify MongoDB is running:**
   ```bash
   docker exec -it mongodb-logs mongosh --eval "db.adminCommand('ping')" -u admin -p SecurePassword123! --authenticationDatabase admin
   ```

2. **Check MongoDB logs:**
   ```bash
   docker logs mongodb-logs
   ```

3. **Verify connection string includes authSource:**
   ```
   mongodb://admin:SecurePassword123!@host.docker.internal:27017/logs?authSource=admin
   ```

### JWT Key Issues

1. **Verify keys are properly encoded:**
   - **Recommended:** Keys should be base64-encoded for Docker environment variables
   - The application automatically detects and decodes base64-encoded keys
   - Base64-encoded keys won't contain `-----BEGIN` markers (they're encoded)
   - Keys must be unencrypted (no password protection) before encoding

2. **Check if key is base64-encoded:**
   ```bash
   # Base64-encoded keys don't start with "-----BEGIN"
   # They look like: LS0tLS1CRUdJTiBSU0EgUFJJVkFURSBLRVktLS0tLQo=
   echo $JWT_PRIVATE_KEY | head -c 50
   ```

3. **Verify base64 encoding/decoding:**
   
   **Windows PowerShell:**
   ```powershell
   # Test decoding (should show PEM format)
   [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($env:JWT_PRIVATE_KEY)) | Select-String "BEGIN"
   ```

   **macOS/Linux:**
   ```bash
   # Test decoding (should show PEM format)
   echo $JWT_PRIVATE_KEY | base64 -d | head -1
   ```

4. **Re-encode keys if needed:**
   
   **Windows PowerShell:**
   ```powershell
   $privateKeyBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content jwt-keys/private_key.pem -Raw)))
   $publicKeyBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Content jwt-keys/public_key.pem -Raw)))
   ```

   **macOS/Linux:**
   ```bash
   cat jwt-keys/private_key.pem | base64 | tr -d '\n'
   cat jwt-keys/public_key.pem | base64 | tr -d '\n'
   ```

5. **Regenerate keys if corrupted:**
   ```bash
   openssl genrsa -out private_key.pem 2048
   openssl rsa -in private_key.pem -pubout -out public_key.pem
   ```

6. **Alternative: Use PEM format directly (not recommended for Docker):**
   - The application also supports plain PEM format (with `-----BEGIN` markers)
   - However, base64 encoding is more reliable for environment variables
   - If using PEM format, ensure newlines are preserved or use `\n` escape sequences

### Database Migration Issues

If you see database migration errors:

1. **Check if database exists:**
   ```bash
   docker exec -it postgres-auth psql -U boilerplate -l
   ```

2. **Run migrations manually (if needed):**
   ```bash
   # Using Makefile (recommended):
   make migrate
   # or
   make run-migration
   
   # Or using dotnet ef directly:
   ConnectionStrings__PostgreSqlConnection="Host=localhost;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!" dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext
   ```

### Clean Up and Start Fresh

If you need to start completely fresh:

**Using Makefile (Recommended):**
```bash
# Stop all services
make docker-down

# Clean generated files (keeps JWT keys)
make clean

# Start fresh setup
make setup
```

**Or manually:**
```bash
# Stop all containers
docker-compose down

# Remove all containers and volumes (WARNING: This deletes all data)
docker-compose down -v

# Remove the Web API image (if built separately)
docker rmi boilerplate-authentication-webapi:latest

# Clean generated files (optional - keeps JWT keys)
make clean

# Start again with full setup
make setup
# Or:
docker-compose up -d --build
```

### Common Issues Summary

1. **Container won't start**: Check logs with `docker logs <container-name>`
2. **Can't connect to database**: Verify connection string uses correct host (`host.docker.internal` for Windows/macOS, service name for docker-compose)
3. **JWT keys not working**: Ensure keys are base64-encoded (recommended) or use PEM format with preserved newlines. Verify encoding with troubleshooting steps above.
4. **MongoDB connection fails**: Verify `MONGODB_CONNECTION_STRING` includes `authSource=admin` parameter
5. **Port conflicts**: Check if ports 5432, 5672, 15672, 27017, 8080, 8081 are already in use

## Next Steps

Once everything is running (via `make setup` or manual setup):

1. **Verify services are running:**
   ```bash
   make verify
   # or
   docker ps
   ```

2. **Access Swagger UI:** http://localhost:8080/swagger

3. **Create an admin user:** The admin user is automatically created by the `AdminUserInitializationService` on startup if it doesn't exist
   - Default username: `admin` (from `ADMIN_USERNAME` environment variable)
   - Default password: `AdminPassword123!` (from `ADMIN_PASSWORD` environment variable)

4. **Generate JWT tokens:** Use the `/oauth/token` endpoint with:
   - Grant type: `password`
   - Username: Your admin username or email
   - Password: Your admin password
   - Tenant ID: The tenant ID from your database

5. **Test API endpoints:** Use Swagger UI with JWT authentication (click "Authorize" and enter your JWT token)

6. **Access other services:**
   - **RabbitMQ Management UI:** http://localhost:15672 (admin/SecurePassword123!)
   - **PostgreSQL:** localhost:5432
   - **MongoDB:** localhost:27017

**For more information on available Makefile commands, see [SETUP.md](SETUP.md).**
5. **Monitor logs:** Check MongoDB logs collection for application logs
6. **Monitor RabbitMQ:** Check the management dashboard for message queues

## Security Notes

⚠️ **Important:** The passwords and connection strings in this guide are for **development only**. For production:

1. Use strong, randomly generated passwords
2. Store secrets in a secrets manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)
3. Use environment variables or Docker secrets (not hardcoded in docker-compose.yml)
4. Enable TLS/SSL for all database connections
5. Use network policies to restrict container communication
6. Regularly rotate passwords and keys
7. Never commit `.env` files or secrets to source control

## Environment Variables Reference

### Required Environment Variables

| Variable Name | Description | Example | Notes |
|--------------|-------------|---------|-------|
| `MONGODB_CONNECTION_STRING` | **REQUIRED** MongoDB connection string for logging | `mongodb://admin:password@host:27017/logs?authSource=admin` | Must include `authSource=admin` if using authentication |
| `JWT_PRIVATE_KEY` | RSA private key for JWT signing (base64-encoded recommended) | `LS0tLS1CRUdJTiBSU0EgUFJJVkFURSBLRVktLS0tLQo...` | **Recommended:** Base64-encoded PEM format. Also supports plain PEM with `-----BEGIN` markers. Must be unencrypted. |
| `JWT_PUBLIC_KEY` | RSA public key for JWT validation (base64-encoded recommended) | `LS0tLS1CRUdJTiBSU0EgUFVCTElDIEtFWS0tLS0tCg...` | **Recommended:** Base64-encoded PEM format. Also supports plain PEM with `-----BEGIN` markers. |

### Optional Environment Variables

| Variable Name | Description | Example | Default |
|--------------|-------------|---------|---------|
| `ConnectionStrings__PostgreSqlConnection` | PostgreSQL connection string | `Host=postgres;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!` | From `appsettings.json` |
| `JWT_EXPIRATION_MINUTES` | JWT token expiration in minutes | `60` | `15` (from `appsettings.json`) |
| `RABBITMQ_CONNECTION_STRING` | RabbitMQ connection string | `amqp://admin:password@rabbitmq:5672/` | `amqp://guest:guest@localhost:5672/` |
| `ADMIN_USERNAME` | Service administrator username | `admin` | `admin` |
| `ADMIN_PASSWORD` | Service administrator password | `AdminPassword123!` | `AdminPassword123!` |

### Connection String Formats

**PostgreSQL (for individual containers - use `host.docker.internal`):**
```
Host=host.docker.internal;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!
```

**PostgreSQL (for docker-compose - use service name):**
```
Host=postgres;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!
```

**RabbitMQ (for individual containers - use `host.docker.internal`):**
```
amqp://admin:SecurePassword123!@host.docker.internal:5672/
```

**RabbitMQ (for docker-compose - use service name):**
```
amqp://admin:SecurePassword123!@rabbitmq:5672/
```

**MongoDB (for individual containers - use `host.docker.internal`):**
```
mongodb://admin:SecurePassword123!@host.docker.internal:27017/logs?authSource=admin
```

**MongoDB (for docker-compose - use service name):**
```
mongodb://admin:SecurePassword123!@mongodb:27017/logs?authSource=admin
```

**Note on `host.docker.internal`:**
- **Windows/macOS**: Use `host.docker.internal` to connect from a container to services on the host machine
- **Linux**: `host.docker.internal` may not work. Use `172.17.0.1` (default Docker bridge IP) or your host's actual IP address
- **Docker Compose**: Services can communicate using service names directly (`postgres`, `rabbitmq`, `mongodb`)

## Port Reference

| Service | Port | Protocol | Access URL |
|---------|------|----------|------------|
| Web API | 8080 | HTTP | http://localhost:8080 |
| Web API (HTTPS) | 8081 | HTTPS | https://localhost:8081 |
| Swagger UI | 8080 | HTTP | http://localhost:8080/swagger |
| PostgreSQL | 5432 | TCP | `localhost:5432` (for external clients) |
| RabbitMQ AMQP | 5672 | AMQP | `localhost:5672` (for clients) |
| RabbitMQ Management | 15672 | HTTP | http://localhost:15672 |
| MongoDB | 27017 | TCP | `localhost:27017` (for external clients) |

## Default Credentials (Development Only)

| Service | Username | Password |
|---------|----------|----------|
| PostgreSQL | `boilerplate` | `SecurePassword123!` |
| RabbitMQ Management UI | `admin` | `SecurePassword123!` |
| MongoDB | `admin` | `SecurePassword123!` |
| Admin User | `admin` (configurable) | `AdminPassword123!` (configurable) |

⚠️ **Security Warning**: Change all passwords before deploying to production!

## Additional Resources

- [Docker Documentation](https://docs.docker.com/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [MongoDB Documentation](https://docs.mongodb.com/)
- [Serilog MongoDB Sink](https://github.com/serilog/serilog-sinks-mongodb)
