# Quick Setup Guide

This guide provides the fastest way to set up the BoilerPlate Authentication project.

## Prerequisites

Run **`./verify-prerequisites.sh docker`** to check all prerequisites. It prints install commands for anything missing.

- **OpenSSL** - For generating JWT keys
- **Docker Desktop** - For running services
- **.NET SDK 8.0+** - For migrations
- **Make** - For Unix (Windows: use `setup.ps1`)

See [PREREQUISITES.md](PREREQUISITES.md) for install commands by platform.

## Quick Start

### Option 1: Using Makefile (macOS/Linux/WSL/Git Bash)

If you have `make` installed, simply run:

```bash
make setup
```

This will:
1. Verify prerequisites (OpenSSL, Docker, Docker Compose)
2. Generate JWT keys if they don't exist
3. Create the `.env` file with base64-encoded keys
4. Display next steps

### Option 2: Using PowerShell Script (Windows)

If you're on Windows without `make`, use the PowerShell script:

```powershell
.\setup.ps1
```

This performs the same setup as the Makefile.

### Option 3: Manual Setup

If you prefer to set up manually, see the [DOCKER_STARTUP_GUIDE.md](DOCKER_STARTUP_GUIDE.md) for detailed instructions.

### Production Deployment

For production hardening (trusted certificates, port lockdown, single-port deployment), see [PRODUCTION_HARDENING.md](PRODUCTION_HARDENING.md).

## Available Makefile Commands

Once you have `make` installed, you can use these commands. Run `make help` to see all available commands with descriptions.

### Setup Commands

| Command | Description |
|---------|-------------|
| `make setup` | **Complete setup workflow**: Verifies prerequisites, generates JWT keys, creates .env file, ensures all services are running, builds the webapi project, runs database migrations, rebuilds the webapi Docker image, and starts the webapi container. This is the recommended command for first-time setup. |
| `make verify-prerequisites` | Verifies that all required tools are installed: OpenSSL, Docker, Docker Compose, .NET SDK 8.0+, and dotnet-ef tool. Automatically installs dotnet-ef if missing. |
| `make setup-keys` | Generates JWT RSA key pairs (2048-bit) if they don't already exist. Creates both `private_key.pem` and `public_key.pem` in the `jwt-keys/` directory. |
| `make setup-env` | Creates or updates the `.env` file with base64-encoded JWT keys and all required connection strings (PostgreSQL, RabbitMQ, MongoDB) and configuration settings. Requires JWT keys to exist (runs `setup-keys` automatically if needed). |

### Service Management Commands

| Command | Description |
|---------|-------------|
| `make ensure-services` | Ensures all required Docker services are running (PostgreSQL, RabbitMQ, MongoDB). Does NOT start the webapi service. Creates containers if they don't exist, starts them if they're stopped. |
| `make ensure-postgres` | Ensures PostgreSQL service is running and ready to accept connections. Waits up to 20 seconds for PostgreSQL to become ready. Creates and starts the container if needed. |
| `make docker-up` | Starts all Docker services defined in `docker-compose.yml` (PostgreSQL, RabbitMQ, MongoDB, WebAPI). Waits 5 seconds for services to become healthy. |
| `make docker-up-webapi` | Starts only the webapi service. Assumes other services (PostgreSQL, RabbitMQ, MongoDB) are already running. Displays service URLs after starting. |
| `make docker-down` | Stops all Docker services and removes containers. Data volumes are preserved. |

### Build and Migration Commands

| Command | Description |
|---------|-------------|
| `make build-webapi-project` | Builds the .NET WebAPI project using `dotnet build` in Release configuration. Verifies .NET SDK is installed before building. |
| `make run-migration` | Runs Entity Framework Core database migrations against PostgreSQL. Requires PostgreSQL to be running and ready. Automatically ensures services are running first. Uses `dotnet ef database update` command. |
| `make migrate` | Alternative command to apply database migrations independently. Can be run without ensuring services first (useful for manual migration runs). |
| `make rebuild-webapi-docker` | Rebuilds the webapi Docker image using cached base images (does not pull base images). Creates the webapi container if it doesn't exist. Waits for dependent services (PostgreSQL, RabbitMQ, MongoDB) to be healthy before creating the container. Uses `DOCKER_BUILDKIT=0` to prevent pulling base images. |
| `make rebuild-webapi` | Alias for `rebuild-webapi-docker`. Provided for convenience. |
| `make docker-build` | Rebuilds the webapi Docker image and then starts the webapi service. Combines `rebuild-webapi-docker` with `docker-up-webapi`. |

### Logging Commands

| Command | Description |
|---------|-------------|
| `make docker-logs` | Views and follows logs from all Docker services in real-time. Use Ctrl+C to stop following logs. |
| `make docker-logs-webapi` | Views and follows logs from only the webapi service. Useful for debugging the API without cluttering output with other services. |

### Utility Commands

| Command | Description |
|---------|-------------|
| `make verify` | Verifies the complete setup by checking: 1) JWT keys exist and are valid, 2) .env file exists with required keys and connection strings, 3) Docker services are running (PostgreSQL, RabbitMQ, MongoDB, WebAPI). Displays status for each check. |
| `make clean` | Removes generated files (`.env` file and base64-encoded key text files) but **preserves JWT key files** (`.pem` files). Useful for resetting configuration without regenerating keys. |
| `make clean-all` | ⚠️ **WARNING**: Removes ALL generated files including JWT keys. Prompts for confirmation before deletion. Use with caution as this will delete your RSA key pairs. |
| `make regen-keys` | Regenerates JWT RSA key pairs. **Backs up existing keys** with `.bak` extension before generating new ones. After regenerating keys, run `make setup-env` to update the `.env` file. |
| `make help` | Displays help message with all available commands and their descriptions. Also shows the detected operating system. |

### Command Dependencies

The `make setup` command executes the following steps in order:

1. `verify-prerequisites` - Checks for required tools
2. `setup-keys` - Generates JWT keys if needed
3. `setup-env` - Creates .env file with configuration
4. `ensure-services` - Ensures PostgreSQL, RabbitMQ, MongoDB are running
5. `build-webapi-project` - Builds the .NET project
6. `run-migration` - Applies database migrations
7. `rebuild-webapi-docker` - Builds Docker image and creates container
8. `docker-up-webapi` - Starts the webapi container

### Common Workflows

**First-time setup:**
```bash
make setup
```

**After code changes (rebuild and restart):**
```bash
make rebuild-webapi-docker
make docker-up-webapi
```

**Apply new migrations:**
```bash
make run-migration
```

**View API logs:**
```bash
make docker-logs-webapi
```

**Stop everything:**
```bash
make docker-down
```

**Clean and start fresh (keeps keys):**
```bash
make clean
make setup
```

**Regenerate keys (if compromised):**
```bash
make regen-keys
make setup-env
make docker-down
make docker-up
```

## Starting Services

After running `make setup` or `.\setup.ps1`, start all services:

```bash
docker-compose up -d
```

Or using make:
```bash
make docker-up
```

## Accessing Services

Once services are running:

- **Frontend**: http://localhost:4200 (or https://localhost:4200 in Docker)
  - Login with admin credentials (see Web API admin setup)
  - Service Administrators: Tenants, RabbitMQ Management (auto-login), Event logs, Audit logs
  - See [BoilerPlate.Frontend/README.md](BoilerPlate.Frontend/README.md) for role-based access details
- **Web API**: http://localhost:8080
- **Swagger UI**: http://localhost:8080/swagger
- **RabbitMQ Management**: http://localhost:15672 (direct) or via frontend link (Service Administrators)
  - Username: `admin`
  - Password: `SecurePassword123!`
- **PostgreSQL**: localhost:5432
- **MongoDB**: localhost:27017

## Troubleshooting

### Makefile not found or 'make' command not recognized

**Windows**: Install `make` via one of these options:
- Use Git Bash (includes make)
- Use WSL (Windows Subsystem for Linux)
- Install via Chocolatey: `choco install make`
- Use the PowerShell script instead: `.\setup.ps1`

**macOS/Linux**: Install make:
- macOS: `xcode-select --install`
- Ubuntu/Debian: `sudo apt-get install build-essential`
- CentOS/RHEL: `sudo yum groupinstall "Development Tools"`

### OpenSSL not found

- Windows: Download from [Win32OpenSSL](https://slproweb.com/products/Win32OpenSSL.html) and add to PATH
- macOS: `brew install openssl`
- Linux: Install via your package manager

### Docker services won't start

1. Verify Docker Desktop is running
2. Check if ports are already in use: `netstat -an | grep -E "(5432|5672|15672|27017|8080)"`
3. View logs: `make docker-logs` or `docker-compose logs`
4. Verify `.env` file exists and contains valid base64-encoded keys

### JWT keys invalid

1. Regenerate keys: `make regen-keys`
2. Recreate .env file: `make setup-env`
3. Restart services: `make docker-down && make docker-up`

## Next Steps

1. Review the generated `.env` file
2. Start services: `make docker-up` or `docker-compose up -d`
3. Access Swagger UI: http://localhost:8080/swagger
4. See [DOCKER_STARTUP_GUIDE.md](DOCKER_STARTUP_GUIDE.md) for more details
