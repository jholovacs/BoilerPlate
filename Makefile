.PHONY: help setup setup-keys setup-env setup-volumes ensure-services ensure-postgres build-webapi-project build-audit-project build-frontend-project run-migration migrate migrate-only rebuild-webapi-image rebuild-webapi-docker rebuild-audit-image rebuild-audit-docker rebuild-frontend-docker docker-up docker-up-webapi docker-up-audit docker-up-frontend docker-down docker-build rebuild-webapi rebuild-audit rebuild-frontend redeploy docker-logs docker-logs-webapi docker-logs-audit docker-logs-frontend clean verify

# Detect OS
UNAME_S := $(shell uname -s)
OS := Unknown

ifeq ($(UNAME_S),Linux)
	OS := Linux
	SHELL := /bin/bash
endif
ifeq ($(UNAME_S),Darwin)
	OS := macOS
	SHELL := /bin/bash
endif
ifneq ($(findstring MINGW,$(UNAME_S)),)
	OS := Windows
	SHELL := /bin/bash
endif
ifneq ($(findstring MSYS,$(UNAME_S)),)
	OS := Windows
	SHELL := /bin/bash
endif
ifneq ($(findstring CYGWIN,$(UNAME_S)),)
	OS := Windows
	SHELL := /bin/bash
endif

# Check if running in WSL
ifdef WSL_DISTRO_NAME
	OS := WSL
	SHELL := /bin/bash
endif

# Default target
.DEFAULT_GOAL := help

# Detect if colors should be used
# Disable colors if:
#   1. NO_COLOR environment variable is set (common convention: https://no-color.org/)
#   2. Output is redirected/piped (not a TTY)
#   3. TERM is not set or is set to "dumb"
#   4. TERM is not a known color-capable terminal
# Enable colors if:
#   1. FORCE_COLOR environment variable is set (force enable)
#   2. TTY detected, TERM is set, and TERM is color-capable
# Check if output is a TTY (not piped/redirected)
# Use a simpler check: test -t 1 checks if stdout is a TTY
TTY_CHECK := $(shell test -t 1 2>/dev/null && echo 1 || echo 0)
NO_COLOR_ENV := $(strip $(NO_COLOR))
FORCE_COLOR_ENV := $(strip $(FORCE_COLOR))

ifeq ($(FORCE_COLOR_ENV),1)
	# Explicitly enabled via FORCE_COLOR environment variable
	COLOR_ENABLED := 1
else ifeq ($(NO_COLOR_ENV),1)
	# Explicitly disabled via NO_COLOR environment variable
	COLOR_ENABLED := 0
else ifeq ($(TTY_CHECK),0)
	# Output is redirected (piped), disable colors
	COLOR_ENABLED := 0
else ifeq ($(TERM),)
	# TERM not set, disable colors
	COLOR_ENABLED := 0
else ifeq ($(TERM),dumb)
	# TERM is "dumb", disable colors
	COLOR_ENABLED := 0
else
	# Check if TERM indicates a color-capable terminal
	# Only enable colors for known color-capable terminals
	# Use case-insensitive check for better compatibility
	TERM_LOWER := $(shell echo "$(TERM)" | tr '[:upper:]' '[:lower:]')
	TERM_CHECK := $(shell case "$(TERM_LOWER)" in \
		xterm*|screen*|tmux*|linux|rxvt*|putty|cygwin|mintty) echo 1 ;; \
		*) echo 0 ;; \
	esac)
	ifeq ($(TERM_CHECK),1)
		# TERM is recognized as color-capable, enable colors
		COLOR_ENABLED := 1
	else
		# TERM not recognized as color-capable, disable colors
		COLOR_ENABLED := 0
	endif
endif

# Colors for output (only if terminal supports it)
ifeq ($(COLOR_ENABLED),1)
	GREEN := \033[0;32m
	YELLOW := \033[1;33m
	RED := \033[0;31m
	NC := \033[0m
else
	GREEN :=
	YELLOW :=
	RED :=
	NC :=
endif

# PostgreSQL credentials (matching docker-compose.yml)
POSTGRES_USER ?= boilerplate
POSTGRES_PASSWORD ?= SecurePassword123!
POSTGRES_DB ?= BoilerPlateAuth
POSTGRES_HOST ?= localhost
POSTGRES_PORT ?= 5432

help: ## Show this help message
	@echo "$(GREEN)BoilerPlate Authentication Setup$(NC)"
	@echo ""
	@echo "$(YELLOW)Usage:$(NC) make [target]"
	@echo ""
	@echo "$(YELLOW)Available targets:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-15s$(NC) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(YELLOW)Detected OS:$(NC) $(OS)"
	@echo ""
	@echo "$(YELLOW)PostgreSQL Configuration:$(NC)"
	@echo "  Host: $(POSTGRES_HOST)"
	@echo "  Port: $(POSTGRES_PORT)"
	@echo "  User: $(POSTGRES_USER)"
	@echo "  Database: $(POSTGRES_DB)"
	@echo "  (Override with: make setup POSTGRES_PASSWORD=YourPassword)"

setup: verify-prerequisites setup-keys setup-env setup-volumes ensure-services build-webapi-project build-audit-project build-frontend-project run-migration rebuild-webapi-docker rebuild-audit-docker rebuild-frontend-docker docker-up-webapi docker-up-audit docker-up-frontend ## Complete setup: create/reset volumes, create services, build projects, run migrations, create images, and start containers
	@echo "$(GREEN)✓ Setup complete!$(NC)"
	@echo ""
	@echo "$(YELLOW)Service URLs:$(NC)"
	@echo "  - Frontend: http://localhost:4200"
	@echo "  - Web API: http://localhost:8080"
	@echo "  - Swagger UI: http://localhost:8080/swagger"
	@echo "  - RabbitMQ Management: http://localhost:15672 (admin/SecurePassword123!)"
	@echo "  - PostgreSQL: localhost:5432"
	@echo "  - MongoDB: localhost:27017"
	@echo "  - OTEL Collector:"
	@echo "    - OTLP gRPC: localhost:4317"
	@echo "    - OTLP HTTP: localhost:4318"
	@echo "    - Prometheus Metrics: http://localhost:8888/metrics"
	@echo ""
	@echo "$(YELLOW)Next steps:$(NC)"
	@echo "  1. Review the .env file (it contains base64-encoded JWT keys)"
	@echo "  2. Access Swagger UI at http://localhost:8080/swagger"
	@echo "  3. All services are now running and ready to use!"

verify-prerequisites: ## Verify required tools are installed
	@echo "$(YELLOW)Verifying prerequisites...$(NC)"
	@which openssl > /dev/null 2>&1 || (echo "$(RED)✗ OpenSSL is not installed. Please install it first.$(NC)" && exit 1)
	@which docker > /dev/null 2>&1 || (echo "$(RED)✗ Docker is not installed. Please install Docker Desktop first.$(NC)" && exit 1)
	@which docker-compose > /dev/null 2>&1 || which docker compose > /dev/null 2>&1 || (echo "$(RED)✗ Docker Compose is not installed. Please install it first.$(NC)" && exit 1)
	@which dotnet > /dev/null 2>&1 || (echo "$(RED)✗ .NET SDK is not installed. Please install .NET SDK 8.0 or later.$(NC)" && exit 1)
	@if ! PATH="$$HOME/.dotnet/tools:$$PATH" dotnet ef --version > /dev/null 2>&1; then \
		echo "$(YELLOW)  Installing dotnet-ef tool...$(NC)"; \
		if dotnet tool install --global dotnet-ef --version 8.0.0 2>&1; then \
			echo "$(GREEN)  ✓ dotnet-ef tool installed$(NC)"; \
		else \
			echo "$(YELLOW)  ⚠ Failed to install dotnet-ef tool automatically.$(NC)"; \
			echo "$(YELLOW)  Please install manually: dotnet tool install --global dotnet-ef --version 8.0.0$(NC)"; \
			echo "$(YELLOW)  Then add to PATH: export PATH=\"$$HOME/.dotnet/tools:$$PATH\"$(NC)"; \
			echo "$(YELLOW)  Migrations will be skipped. Run 'make migrate' manually after installing dotnet-ef$(NC)"; \
		fi; \
	fi
	@if ! PATH="$$HOME/.dotnet/tools:$$PATH" dotnet ef --version > /dev/null 2>&1; then \
		echo "$(YELLOW)  ⚠ dotnet-ef tool is not available. Migrations will be skipped.$(NC)"; \
		echo "$(YELLOW)  Install with: dotnet tool install --global dotnet-ef --version 8.0.0$(NC)"; \
		echo "$(YELLOW)  Then add to PATH: export PATH=\"$$HOME/.dotnet/tools:$$PATH\"$(NC)"; \
	else \
		echo "$(GREEN)  ✓ dotnet-ef tool is available$(NC)"; \
	fi
	@echo "$(GREEN)✓ All prerequisites are installed$(NC)"

setup-keys: ## Generate JWT keys if they don't exist
	@echo "$(YELLOW)Setting up JWT keys...$(NC)"
	@mkdir -p jwt-keys
	@if [ ! -f jwt-keys/private_key.pem ]; then \
		echo "$(YELLOW)  Generating private key...$(NC)"; \
		openssl genrsa -out jwt-keys/private_key.pem 2048; \
		echo "$(GREEN)  ✓ Private key generated$(NC)"; \
	else \
		echo "$(GREEN)  ✓ Private key already exists$(NC)"; \
	fi
	@if [ ! -f jwt-keys/public_key.pem ]; then \
		echo "$(YELLOW)  Generating public key...$(NC)"; \
		openssl rsa -in jwt-keys/private_key.pem -pubout -out jwt-keys/public_key.pem; \
		echo "$(GREEN)  ✓ Public key generated$(NC)"; \
	else \
		echo "$(GREEN)  ✓ Public key already exists$(NC)"; \
	fi

setup-volumes: ## Create or reset Docker volumes for PostgreSQL, MongoDB, RabbitMQ, and OTEL Collector
	@echo "$(YELLOW)Setting up Docker volumes...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  ⚠ Docker is not running. Skipping volume setup.$(NC)"; \
		echo "$(YELLOW)  Start Docker Desktop and volumes will be created automatically when services start$(NC)"; \
		exit 0; \
	fi
	@echo "$(YELLOW)  Stopping and removing containers that use these volumes...$(NC)"
	@docker stop postgres-auth mongodb-logs rabbitmq-auth otel-collector 2>/dev/null || true
	@docker rm -f postgres-auth mongodb-logs rabbitmq-auth otel-collector 2>/dev/null || true
	@if command -v docker-compose > /dev/null 2>&1; then \
		docker-compose stop postgres mongodb rabbitmq otel-collector 2>/dev/null || true; \
		docker-compose rm -f postgres mongodb rabbitmq otel-collector 2>/dev/null || true; \
		echo "$(YELLOW)  Using docker-compose to remove volumes...$(NC)"; \
		docker-compose down -v 2>&1 | grep -v "No such" || true; \
	elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then \
		docker compose stop postgres mongodb rabbitmq otel-collector 2>/dev/null || true; \
		docker compose rm -f postgres mongodb rabbitmq otel-collector 2>/dev/null || true; \
		echo "$(YELLOW)  Using docker compose to remove volumes...$(NC)"; \
		docker compose down -v 2>&1 | grep -v "No such" || true; \
	fi
	@sleep 2
	@echo "$(YELLOW)  Finding all matching volumes (including project-prefixed ones)...$(NC)"
	@ALL_MATCHING_VOLUMES=$$(docker volume ls -q 2>/dev/null | grep -iE "(postgres_data|mongodb_data|rabbitmq_data|otel_collector_data)" || true); \
	if [ -n "$$ALL_MATCHING_VOLUMES" ]; then \
		echo "$(YELLOW)    Found volumes to remove:$$(echo "$$ALL_MATCHING_VOLUMES" | tr '\n' ' ')$(NC)"; \
		for vol_full_name in $$ALL_MATCHING_VOLUMES; do \
			echo "$(YELLOW)    Removing volume: $$vol_full_name$(NC)"; \
			CONTAINERS=$$(docker ps -a -q --filter volume=$$vol_full_name 2>/dev/null || true); \
			if [ -n "$$CONTAINERS" ]; then \
				echo "$(YELLOW)      Removing containers using this volume: $$CONTAINERS$(NC)"; \
				for cid in $$CONTAINERS; do \
					docker rm -f $$cid 2>/dev/null || true; \
				done; \
				sleep 1; \
			fi; \
			if docker volume rm $$vol_full_name 2>/dev/null; then \
				echo "$(GREEN)      ✓ $$vol_full_name removed$(NC)"; \
			else \
				ERR=$$(docker volume rm $$vol_full_name 2>&1 || true); \
				if ! docker volume inspect $$vol_full_name > /dev/null 2>&1; then \
					echo "$(GREEN)      ✓ $$vol_full_name removed$(NC)"; \
				else \
					echo "$(RED)      ✗ Failed: $$ERR$(NC)"; \
				fi; \
			fi; \
		done; \
	else \
		echo "$(GREEN)    ✓ No matching volumes found$(NC)"; \
	fi
	@echo "$(YELLOW)  Removing volumes by exact name (postgres_data, mongodb_data, rabbitmq_data, otel_collector_data)...$(NC)"
	@for vol in postgres_data mongodb_data rabbitmq_data otel_collector_data; do \
		if docker volume inspect $$vol > /dev/null 2>&1; then \
			echo "$(YELLOW)    Removing $$vol...$(NC)"; \
			CONTAINERS=$$(docker ps -a -q --filter volume=$$vol 2>/dev/null || true); \
			if [ -n "$$CONTAINERS" ]; then \
				echo "$(YELLOW)      Removing containers: $$CONTAINERS$(NC)"; \
				for cid in $$CONTAINERS; do \
					docker rm -f $$cid 2>/dev/null || true; \
				done; \
				sleep 1; \
			fi; \
			if docker volume rm $$vol 2>/dev/null; then \
				echo "$(GREEN)      ✓ $$vol removed$(NC)"; \
			else \
				if ! docker volume inspect $$vol > /dev/null 2>&1; then \
					echo "$(GREEN)      ✓ $$vol removed$(NC)"; \
				else \
					ERR_MSG=$$(docker volume rm $$vol 2>&1 || true); \
					echo "$(RED)      ✗ Failed to remove $$vol$(NC)"; \
					echo "$(YELLOW)      Error: $$ERR_MSG$(NC)"; \
					echo "$(RED)      ⚠ OLD DATA WILL PERSIST in $$vol$(NC)"; \
					echo "$(YELLOW)      Run manually: docker volume rm $$vol$(NC)"; \
				fi; \
			fi; \
		fi; \
	done
	@sleep 1
	@sleep 1
	@echo "$(YELLOW)  Verifying all volumes are removed...$(NC)"
	@VOLUMES_REMAINING=$$(docker volume ls -q 2>/dev/null | grep -iE "^(postgres_data|mongodb_data|rabbitmq_data|otel_collector_data)$$" || true); \
	if [ -n "$$VOLUMES_REMAINING" ]; then \
		echo "$(RED)⚠ WARNING: Some volumes still exist with old data!$(NC)"; \
		echo "$(RED)  These volumes will NOT be reset and old data will persist:$(NC)"; \
		for v in $$VOLUMES_REMAINING; do \
			echo "$(RED)    - $$v$(NC)"; \
		done; \
		echo ""; \
		echo "$(YELLOW)  To manually remove them, run:$(NC)"; \
		for v in $$VOLUMES_REMAINING; do \
			echo "$(YELLOW)    docker volume rm $$v$(NC)"; \
		done; \
		echo "$(YELLOW)  Then run 'make setup-volumes' again$(NC)"; \
		echo ""; \
	else \
		echo "$(GREEN)    ✓ All volumes successfully removed$(NC)"; \
	fi
	@echo "$(YELLOW)  Creating fresh volumes...$(NC)"
	@for vol in postgres_data mongodb_data rabbitmq_data otel_collector_data; do \
		if docker volume inspect $$vol > /dev/null 2>&1; then \
			echo "$(YELLOW)    ⚠ $$vol already exists (old data may persist)$(NC)"; \
			echo "$(YELLOW)    Skipping creation - using existing volume$(NC)"; \
		else \
			if docker volume create $$vol > /dev/null 2>&1; then \
				echo "$(GREEN)    ✓ $$vol created$(NC)"; \
			else \
				echo "$(RED)    ✗ Failed to create $$vol$(NC)"; \
			fi; \
		fi; \
	done
	@echo "$(GREEN)✓ Volume setup complete$(NC)"

setup-env: setup-keys ## Create .env file with base64-encoded JWT keys
	@echo "$(YELLOW)Creating .env file...$(NC)"
	@if [ ! -f jwt-keys/private_key.pem ] || [ ! -f jwt-keys/public_key.pem ]; then \
		echo "$(RED)  ✗ JWT keys not found. Run 'make setup-keys' first.$(NC)"; \
		exit 1; \
	fi
	@echo "$(YELLOW)  Encoding keys as base64...$(NC)"
	@if command -v base64 > /dev/null 2>&1; then \
		cat jwt-keys/private_key.pem | base64 | tr -d '\n' | tr -d '\r' > jwt-keys/private_key_base64.txt && \
		cat jwt-keys/public_key.pem | base64 | tr -d '\n' | tr -d '\r' > jwt-keys/public_key_base64.txt; \
	elif command -v openssl > /dev/null 2>&1; then \
		openssl base64 -in jwt-keys/private_key.pem -A | tr -d '\n' | tr -d '\r' > jwt-keys/private_key_base64.txt && \
		openssl base64 -in jwt-keys/public_key.pem -A | tr -d '\n' | tr -d '\r' > jwt-keys/public_key_base64.txt; \
	else \
		echo "$(RED)  ✗ Neither base64 nor openssl found for encoding$(NC)"; \
		echo "$(YELLOW)  Trying PowerShell on Windows...$(NC)"; \
		if command -v powershell.exe > /dev/null 2>&1 && [ -f setup-env.ps1 ]; then \
			powershell.exe -ExecutionPolicy Bypass -File setup-env.ps1; \
		else \
			echo "$(RED)  ✗ No suitable encoding tool found$(NC)"; \
			echo "$(YELLOW)  Please install OpenSSL or run setup.ps1 manually on Windows$(NC)"; \
			exit 1; \
		fi; \
	fi
	@if [ -f jwt-keys/private_key_base64.txt ] && [ -f jwt-keys/public_key_base64.txt ]; then \
		echo "JWT_PRIVATE_KEY=$$(cat jwt-keys/private_key_base64.txt | tr -d '\r')" > .env; \
		echo "JWT_PUBLIC_KEY=$$(cat jwt-keys/public_key_base64.txt | tr -d '\r')" >> .env; \
		echo "JWT_EXPIRATION_MINUTES=60" >> .env; \
		echo "" >> .env; \
		echo "# Database Connection Strings" >> .env; \
		echo "ConnectionStrings__PostgreSqlConnection=Host=postgres;Port=5432;Database=$(POSTGRES_DB);Username=$(POSTGRES_USER);Password=$(POSTGRES_PASSWORD)" >> .env; \
		echo "" >> .env; \
		echo "# Service Bus Connection Strings" >> .env; \
		echo "RABBITMQ_CONNECTION_STRING=amqp://admin:SecurePassword123!@rabbitmq:5672/" >> .env; \
		echo "" >> .env; \
		echo "# MongoDB Connection String" >> .env; \
		echo "MONGODB_CONNECTION_STRING=mongodb://admin:SecurePassword123!@mongodb:27017/logs?authSource=admin" >> .env; \
		echo "" >> .env; \
		echo "# OpenTelemetry Collector Connection String" >> .env; \
		echo "OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317" >> .env; \
		echo "" >> .env; \
		echo "# Admin User Configuration" >> .env; \
		echo "ADMIN_USERNAME=admin" >> .env; \
		echo "ADMIN_PASSWORD=AdminPassword123!" >> .env; \
		echo "$(GREEN)  ✓ .env file created with all connection strings$(NC)"; \
	else \
		echo "$(RED)  ✗ Failed to create base64 encoded keys$(NC)"; \
		exit 1; \
	fi

ensure-services: ## Ensure all required services are running (PostgreSQL, RabbitMQ, MongoDB, OTEL Collector, but NOT webapi)
	@echo "$(YELLOW)Ensuring required services are running...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  ⚠ Docker is not running. Please start Docker Desktop first.$(NC)"; \
		exit 1; \
	fi
	@# Ensure PostgreSQL is running
	@$(MAKE) ensure-postgres
	@# Ensure RabbitMQ is running
	@echo "$(YELLOW)Ensuring RabbitMQ is running...$(NC)"
	@if docker ps --filter "name=rabbitmq-auth" --format "{{.Names}}" | grep -q "rabbitmq-auth"; then \
		echo "$(GREEN)  ✓ RabbitMQ container is already running$(NC)"; \
	elif docker ps -a --filter "name=rabbitmq-auth" --format "{{.Names}}" | grep -q "rabbitmq-auth"; then \
		echo "$(YELLOW)  Starting existing RabbitMQ container...$(NC)"; \
		docker-compose start rabbitmq > /dev/null 2>&1 || docker compose start rabbitmq > /dev/null 2>&1 || docker start rabbitmq-auth > /dev/null 2>&1 || true; \
		echo "$(GREEN)  ✓ RabbitMQ started$(NC)"; \
	else \
		echo "$(YELLOW)  Creating and starting RabbitMQ container...$(NC)"; \
		docker-compose up -d rabbitmq > /dev/null 2>&1 || docker compose up -d rabbitmq > /dev/null 2>&1 || { \
			echo "$(YELLOW)  ⚠ Could not start RabbitMQ automatically.$(NC)"; \
			exit 1; \
		}; \
		echo "$(GREEN)  ✓ RabbitMQ started$(NC)"; \
	fi
	@# Ensure MongoDB is running
	@echo "$(YELLOW)Ensuring MongoDB is running...$(NC)"
	@if docker ps --filter "name=mongodb-logs" --format "{{.Names}}" | grep -q "mongodb-logs"; then \
		echo "$(GREEN)  ✓ MongoDB container is already running$(NC)"; \
	elif docker ps -a --filter "name=mongodb-logs" --format "{{.Names}}" | grep -q "mongodb-logs"; then \
		echo "$(YELLOW)  Starting existing MongoDB container...$(NC)"; \
		docker-compose start mongodb > /dev/null 2>&1 || docker compose start mongodb > /dev/null 2>&1 || docker start mongodb-logs > /dev/null 2>&1 || true; \
		echo "$(GREEN)  ✓ MongoDB started$(NC)"; \
	else \
		echo "$(YELLOW)  Creating and starting MongoDB container...$(NC)"; \
		docker-compose up -d mongodb > /dev/null 2>&1 || docker compose up -d mongodb > /dev/null 2>&1 || { \
			echo "$(YELLOW)  ⚠ Could not start MongoDB automatically.$(NC)"; \
			exit 1; \
		}; \
		echo "$(GREEN)  ✓ MongoDB started$(NC)"; \
	fi
	@# Ensure OTEL Collector is running
	@echo "$(YELLOW)Ensuring OTEL Collector is running...$(NC)"
	@if docker ps --filter "name=otel-collector" --format "{{.Names}}" | grep -q "otel-collector"; then \
		echo "$(GREEN)  ✓ OTEL Collector container is already running$(NC)"; \
	elif docker ps -a --filter "name=otel-collector" --format "{{.Names}}" | grep -q "otel-collector"; then \
		echo "$(YELLOW)  Starting existing OTEL Collector container...$(NC)"; \
		docker-compose start otel-collector > /dev/null 2>&1 || docker compose start otel-collector > /dev/null 2>&1 || docker start otel-collector > /dev/null 2>&1 || true; \
		echo "$(GREEN)  ✓ OTEL Collector started$(NC)"; \
	else \
		echo "$(YELLOW)  Creating and starting OTEL Collector container...$(NC)"; \
		if ! docker image inspect otel/opentelemetry-collector-contrib:latest > /dev/null 2>&1; then \
			echo "$(YELLOW)  Pulling OTEL Collector image...$(NC)"; \
			docker pull otel/opentelemetry-collector-contrib:latest > /dev/null 2>&1 || { \
				echo "$(RED)  ✗ Failed to pull OTEL Collector image. Check your internet connection.$(NC)"; \
				exit 1; \
			}; \
		fi; \
		docker-compose up -d otel-collector > /dev/null 2>&1 || docker compose up -d otel-collector > /dev/null 2>&1 || { \
			echo "$(YELLOW)  ⚠ Could not start OTEL Collector automatically.$(NC)"; \
			exit 1; \
		}; \
		echo "$(GREEN)  ✓ OTEL Collector started$(NC)"; \
	fi
	@echo "$(GREEN)✓ All required services are running$(NC)"

build-webapi-project: ## Build the webapi .NET project (dotnet build)
	@echo "$(YELLOW)Building webapi .NET project...$(NC)"
	@if ! command -v dotnet > /dev/null 2>&1; then \
		echo "$(RED)  ✗ .NET SDK is not installed. Please install .NET SDK 8.0 or later.$(NC)"; \
		exit 1; \
	fi
	@if dotnet build BoilerPlate.Authentication.WebApi/BoilerPlate.Authentication.WebApi.csproj -c Release 2>&1; then \
		echo "$(GREEN)  ✓ WebAPI project built successfully$(NC)"; \
	else \
		echo "$(RED)  ✗ WebAPI project build failed$(NC)"; \
		exit 1; \
	fi

build-audit-project: ## Build the audit service .NET project (dotnet build)
	@echo "$(YELLOW)Building audit service .NET project...$(NC)"
	@if ! command -v dotnet > /dev/null 2>&1; then \
		echo "$(RED)  ✗ .NET SDK is not installed. Please install .NET SDK 8.0 or later.$(NC)"; \
		exit 1; \
	fi
	@if dotnet build BoilerPlate.Services.Audit/BoilerPlate.Services.Audit.csproj -c Release 2>&1; then \
		echo "$(GREEN)  ✓ Audit service project built successfully$(NC)"; \
	else \
		echo "$(RED)  ✗ Audit service project build failed$(NC)"; \
		exit 1; \
	fi

build-frontend-project: ## Build the frontend Angular project (npm install and build)
	@echo "$(YELLOW)Building frontend Angular project...$(NC)"
	@if ! command -v node > /dev/null 2>&1; then \
		echo "$(RED)  ✗ Node.js is not installed. Please install Node.js 18+ first.$(NC)"; \
		echo "$(YELLOW)  Frontend build will be skipped. Docker build will handle it.$(NC)"; \
		exit 0; \
	fi
	@if ! command -v npm > /dev/null 2>&1; then \
		echo "$(RED)  ✗ npm is not installed. Please install npm first.$(NC)"; \
		echo "$(YELLOW)  Frontend build will be skipped. Docker build will handle it.$(NC)"; \
		exit 0; \
	fi
	@if [ ! -d "BoilerPlate.Frontend" ]; then \
		echo "$(YELLOW)  ⚠ Frontend directory not found. Skipping frontend build.$(NC)"; \
		exit 0; \
	fi
	@cd BoilerPlate.Frontend && \
	if [ ! -d "node_modules" ]; then \
		echo "$(YELLOW)  Installing npm dependencies...$(NC)"; \
		npm install 2>&1 || { \
			echo "$(YELLOW)  ⚠ npm install failed. Docker build will handle dependencies.$(NC)"; \
			exit 0; \
		}; \
	fi && \
	echo "$(YELLOW)  Building Angular application...$(NC)" && \
	npm run build 2>&1 || { \
		echo "$(YELLOW)  ⚠ npm build failed. Docker build will handle it.$(NC)"; \
		exit 0; \
	} && \
	echo "$(GREEN)  ✓ Frontend project built successfully$(NC)"

ensure-postgres: ## Ensure PostgreSQL service is running (starts it if needed)
	@echo "$(YELLOW)Ensuring PostgreSQL is running...$(NC)"
	@if docker ps --filter "name=postgres-auth" --format "{{.Names}}" | grep -q "postgres-auth"; then \
		echo "$(GREEN)  ✓ PostgreSQL container is already running$(NC)"; \
		if PGPASSWORD="$(POSTGRES_PASSWORD)" docker exec -e PGPASSWORD="$(POSTGRES_PASSWORD)" postgres-auth pg_isready -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
			echo "$(GREEN)  ✓ PostgreSQL is ready to accept connections$(NC)"; \
		else \
			echo "$(YELLOW)  Waiting for PostgreSQL to be ready...$(NC)"; \
			for i in 1 2 3 4 5; do \
				if PGPASSWORD="$(POSTGRES_PASSWORD)" docker exec -e PGPASSWORD="$(POSTGRES_PASSWORD)" postgres-auth pg_isready -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
					echo "$(GREEN)  ✓ PostgreSQL is ready$(NC)"; \
					break; \
				fi; \
				if [ $$i -lt 5 ]; then \
					echo "$(YELLOW)  Waiting... (attempt $$i/5)$(NC)"; \
					sleep 2; \
				fi; \
			done; \
		fi; \
	elif command -v pg_isready > /dev/null 2>&1 && PGPASSWORD="$(POSTGRES_PASSWORD)" pg_isready -h "$(POSTGRES_HOST)" -p "$(POSTGRES_PORT)" -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
		echo "$(GREEN)  ✓ PostgreSQL is accessible locally (not in Docker)$(NC)"; \
	else \
		echo "$(YELLOW)  PostgreSQL is not running. Starting PostgreSQL container...$(NC)"; \
		if docker ps -a --filter "name=postgres-auth" --format "{{.Names}}" | grep -q "postgres-auth"; then \
			echo "$(YELLOW)  Starting existing PostgreSQL container...$(NC)"; \
			docker start postgres-auth > /dev/null 2>&1 || docker-compose start postgres > /dev/null 2>&1 || docker compose start postgres > /dev/null 2>&1 || true; \
		else \
			echo "$(YELLOW)  Creating and starting PostgreSQL container...$(NC)"; \
			docker-compose up -d postgres > /dev/null 2>&1 || docker compose up -d postgres > /dev/null 2>&1 || { \
				echo "$(YELLOW)  ⚠ Could not start PostgreSQL automatically.$(NC)"; \
				echo "$(YELLOW)  Please run 'make docker-up' or 'docker-compose up -d postgres' manually$(NC)"; \
				exit 0; \
			}; \
		fi; \
		echo "$(YELLOW)  Waiting for PostgreSQL to be ready (this may take 10-20 seconds)...$(NC)"; \
		for i in 1 2 3 4 5 6 7 8 9 10; do \
			if PGPASSWORD="$(POSTGRES_PASSWORD)" docker exec -e PGPASSWORD="$(POSTGRES_PASSWORD)" postgres-auth pg_isready -U "$(POSTGRES_USER)" > /dev/null 2>&1 || PGPASSWORD="$(POSTGRES_PASSWORD)" pg_isready -h "$(POSTGRES_HOST)" -p "$(POSTGRES_PORT)" -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
				echo "$(GREEN)  ✓ PostgreSQL is ready$(NC)"; \
				break; \
			fi; \
			if [ $$i -lt 10 ]; then \
				echo "$(YELLOW)  Waiting for PostgreSQL... (attempt $$i/10)$(NC)"; \
				sleep 2; \
			fi; \
			if [ $$i -eq 10 ]; then \
				echo "$(YELLOW)  ⚠ PostgreSQL did not become ready in time.$(NC)"; \
				echo "$(YELLOW)  Migrations may fail. Run 'make migrate' manually once PostgreSQL is ready$(NC)"; \
			fi; \
		done; \
	fi

run-migration: ensure-services ## Run database migrations (requires PostgreSQL to be running and ready)
	@echo "$(YELLOW)Running database migrations...$(NC)"
	@if [ ! -f .env ]; then \
		echo "$(RED)  ✗ .env file not found. Run 'make setup-env' first.$(NC)"; \
		exit 1; \
	fi
	@if ! PATH="$$HOME/.dotnet/tools:$$PATH" dotnet ef --version > /dev/null 2>&1; then \
		echo "$(YELLOW)  ⚠ dotnet-ef tool is not available. Skipping migration.$(NC)"; \
		echo "$(YELLOW)  Install with: dotnet tool install --global dotnet-ef --version 8.0.0$(NC)"; \
		echo "$(YELLOW)  Then add to PATH: export PATH=\"$$HOME/.dotnet/tools:$$PATH\"$(NC)"; \
		echo "$(YELLOW)  Run 'make migrate' manually after fixing the dotnet-ef tool$(NC)"; \
		exit 0; \
	fi
	@echo "$(YELLOW)  Verifying PostgreSQL is ready for connections...$(NC)"
	@if ! PGPASSWORD="$(POSTGRES_PASSWORD)" docker exec -e PGPASSWORD="$(POSTGRES_PASSWORD)" postgres-auth pg_isready -U "$(POSTGRES_USER)" > /dev/null 2>&1 && ! PGPASSWORD="$(POSTGRES_PASSWORD)" pg_isready -h "$(POSTGRES_HOST)" -p "$(POSTGRES_PORT)" -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
		echo "$(YELLOW)  ⚠ PostgreSQL is not ready. Waiting a bit longer...$(NC)"; \
		sleep 3; \
		if ! PGPASSWORD="$(POSTGRES_PASSWORD)" docker exec -e PGPASSWORD="$(POSTGRES_PASSWORD)" postgres-auth pg_isready -U "$(POSTGRES_USER)" > /dev/null 2>&1 && ! PGPASSWORD="$(POSTGRES_PASSWORD)" pg_isready -h "$(POSTGRES_HOST)" -p "$(POSTGRES_PORT)" -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
			echo "$(YELLOW)  ⚠ PostgreSQL is still not ready. Skipping migrations.$(NC)"; \
			echo "$(YELLOW)  Run 'make migrate' manually once PostgreSQL is ready$(NC)"; \
			exit 0; \
		fi; \
	fi
	@echo "$(GREEN)  ✓ PostgreSQL is ready for migrations$(NC)"
	@echo "$(YELLOW)  Applying migrations with credentials...$(NC)"
	@echo "$(YELLOW)  Connection: Host=$(POSTGRES_HOST), Port=$(POSTGRES_PORT), Database=$(POSTGRES_DB), User=$(POSTGRES_USER)$(NC)"
	@if PATH="$$HOME/.dotnet/tools:$$PATH" PGPASSWORD="$(POSTGRES_PASSWORD)" ConnectionStrings__PostgreSqlConnection="Host=$(POSTGRES_HOST);Port=$(POSTGRES_PORT);Database=$(POSTGRES_DB);Username=$(POSTGRES_USER);Password=$(POSTGRES_PASSWORD)" dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext 2>&1; then \
		echo "$(GREEN)  ✓ Migrations applied successfully$(NC)"; \
	else \
		MIGRATION_EXIT=$$?; \
		echo ""; \
		echo "$(YELLOW)  ⚠ Migration failed (exit code: $$MIGRATION_EXIT)$(NC)"; \
		echo "$(YELLOW)  Check PostgreSQL connection and credentials$(NC)"; \
		echo "$(YELLOW)  Override credentials: make migrate POSTGRES_PASSWORD=YourPassword$(NC)"; \
		exit 0; \
	fi

migrate: ## Apply database migrations (can be run independently)
	@echo "$(YELLOW)Applying database migrations...$(NC)"
	@echo "$(YELLOW)  Connection: Host=$(POSTGRES_HOST), Port=$(POSTGRES_PORT), Database=$(POSTGRES_DB), User=$(POSTGRES_USER)$(NC)"
	@PATH="$$HOME/.dotnet/tools:$$PATH" PGPASSWORD="$(POSTGRES_PASSWORD)" ConnectionStrings__PostgreSqlConnection="Host=$(POSTGRES_HOST);Port=$(POSTGRES_PORT);Database=$(POSTGRES_DB);Username=$(POSTGRES_USER);Password=$(POSTGRES_PASSWORD)" dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext 2>&1 | sed '/MONGODB_CONNECTION_STRING/d' || true
	@echo "$(GREEN)✓ Migration command completed$(NC)"

migrate-only: ## Apply database migrations without ensuring services (assumes PostgreSQL is already running)
	@echo "$(YELLOW)Applying database migrations (assuming PostgreSQL is already running)...$(NC)"
	@if [ ! -f .env ]; then \
		echo "$(RED)  ✗ .env file not found. Run 'make setup-env' first.$(NC)"; \
		exit 1; \
	fi
	@if ! PATH="$$HOME/.dotnet/tools:$$PATH" dotnet ef --version > /dev/null 2>&1; then \
		echo "$(RED)  ✗ dotnet-ef tool is not available. Please install it first.$(NC)"; \
		echo "$(YELLOW)  Install with: dotnet tool install --global dotnet-ef --version 8.0.0$(NC)"; \
		echo "$(YELLOW)  Then add to PATH: export PATH=\"$$HOME/.dotnet/tools:$$PATH\"$(NC)"; \
		exit 1; \
	fi
	@echo "$(YELLOW)  Connection: Host=$(POSTGRES_HOST), Port=$(POSTGRES_PORT), Database=$(POSTGRES_DB), User=$(POSTGRES_USER)$(NC)"
	@if PATH="$$HOME/.dotnet/tools:$$PATH" PGPASSWORD="$(POSTGRES_PASSWORD)" ConnectionStrings__PostgreSqlConnection="Host=$(POSTGRES_HOST);Port=$(POSTGRES_PORT);Database=$(POSTGRES_DB);Username=$(POSTGRES_USER);Password=$(POSTGRES_PASSWORD)" dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext 2>&1 | sed '/MONGODB_CONNECTION_STRING/d'; then \
		echo "$(GREEN)  ✓ Migrations applied successfully$(NC)"; \
	else \
		MIGRATION_EXIT=$$?; \
		echo "$(RED)  ✗ Migration failed (exit code: $$MIGRATION_EXIT)$(NC)"; \
		echo "$(YELLOW)  Make sure PostgreSQL is running and accessible$(NC)"; \
		echo "$(YELLOW)  Override credentials: make migrate-only POSTGRES_PASSWORD=YourPassword$(NC)"; \
		exit 1; \
	fi

docker-up: ## Start all Docker services
	@echo "$(YELLOW)Starting Docker services...$(NC)"
	@docker-compose up -d || docker compose up -d
	@echo "$(GREEN)✓ Services started$(NC)"
	@echo "$(YELLOW)Waiting for services to be healthy...$(NC)"
	@sleep 5
	@echo "$(GREEN)✓ Services are running$(NC)"

docker-up-webapi: ## Start only the webapi service (assumes other services are already running)
	@echo "$(YELLOW)Starting webapi service...$(NC)"
	@if command -v docker-compose > /dev/null 2>&1; then \
		docker-compose up -d --no-deps webapi 2>&1 || { \
			echo "$(YELLOW)  Falling back to starting without --no-deps...$(NC)"; \
			docker-compose up -d webapi 2>&1; \
		}; \
	elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then \
		docker compose up -d --no-deps webapi 2>&1 || { \
			echo "$(YELLOW)  Falling back to starting without --no-deps...$(NC)"; \
			docker compose up -d webapi 2>&1; \
		}; \
	else \
		echo "$(RED)  ✗ Docker Compose is not available$(NC)"; \
		exit 1; \
	fi
	@echo "$(GREEN)✓ WebAPI service started$(NC)"

docker-up-audit: ## Start only the audit service (assumes other services are already running)
	@echo "$(YELLOW)Starting audit service...$(NC)"
	@if command -v docker-compose > /dev/null 2>&1; then \
		docker-compose up -d --no-deps audit 2>&1 || { \
			echo "$(YELLOW)  Falling back to starting without --no-deps...$(NC)"; \
			docker-compose up -d audit 2>&1; \
		}; \
	elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then \
		docker compose up -d --no-deps audit 2>&1 || { \
			echo "$(YELLOW)  Falling back to starting without --no-deps...$(NC)"; \
			docker compose up -d audit 2>&1; \
		}; \
	else \
		echo "$(RED)  ✗ Docker Compose is not available$(NC)"; \
		exit 1; \
	fi
	@echo "$(GREEN)✓ Audit service started$(NC)"

docker-up-frontend: ## Start only the frontend service (assumes other services are already running)
	@echo "$(YELLOW)Starting frontend service...$(NC)"
	@if command -v docker-compose > /dev/null 2>&1; then \
		docker-compose up -d --no-deps frontend 2>&1 || { \
			echo "$(YELLOW)  Falling back to starting without --no-deps...$(NC)"; \
			docker-compose up -d frontend 2>&1; \
		}; \
	elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then \
		docker compose up -d --no-deps frontend 2>&1 || { \
			echo "$(YELLOW)  Falling back to starting without --no-deps...$(NC)"; \
			docker compose up -d frontend 2>&1; \
		}; \
	else \
		echo "$(RED)  ✗ Docker Compose is not available$(NC)"; \
		exit 1; \
	fi
	@echo "$(GREEN)✓ Frontend service started$(NC)"

docker-down: ## Stop all Docker services
	@echo "$(YELLOW)Stopping Docker services...$(NC)"
	@docker-compose down || docker compose down
	@echo "$(GREEN)✓ Services stopped$(NC)"

rebuild-webapi-docker: ensure-services ## Build the webapi Docker image and create the container (uses cached base images, only rebuilding code changes)
	@echo "$(YELLOW)Rebuilding webapi Docker image...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  ⚠ Docker is not running. Skipping image rebuild.$(NC)"; \
		echo "$(YELLOW)  Start Docker Desktop and run 'make docker-build' to rebuild the image$(NC)"; \
		exit 1; \
	fi
	@if docker ps --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api"; then \
		echo "$(YELLOW)  Stopping existing webapi container...$(NC)"; \
		docker-compose stop webapi 2>/dev/null || docker compose stop webapi 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building webapi image (using cached base images, only rebuilding code changes)...$(NC)"
	@echo "$(YELLOW)  Using DOCKER_BUILDKIT=0 to use legacy build (prevents pulling base images)$(NC)"
	@echo "$(YELLOW)  If base images are not cached, this will fail. Cache them first with:$(NC)"
	@echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"
	@echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"
	@if command -v docker > /dev/null 2>&1; then \
		if DOCKER_BUILDKIT=0 docker build --pull=false -f BoilerPlate.Authentication.WebApi/Dockerfile -t boilerplate-authentication-webapi:latest . 2>&1; then \
			echo "$(GREEN)  ✓ WebAPI image built successfully (using cached base images)$(NC)"; \
			echo "$(YELLOW)  Creating webapi container (not starting it)...$(NC)"; \
			if docker ps -a --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api"; then \
				echo "$(GREEN)  ✓ WebAPI container already exists$(NC)"; \
			else \
				echo "$(YELLOW)  Waiting for dependent services to be healthy (checking Docker health status)...$(NC)"; \
				for i in 1 2 3 4 5 6 7 8 9 10; do \
					POSTGRES_HEALTH=$$(docker inspect --format='{{.State.Health.Status}}' postgres-auth 2>/dev/null || echo "starting"); \
					RABBITMQ_HEALTH=$$(docker inspect --format='{{.State.Health.Status}}' rabbitmq-auth 2>/dev/null || echo "starting"); \
					MONGODB_HEALTH=$$(docker inspect --format='{{.State.Health.Status}}' mongodb-logs 2>/dev/null || echo "starting"); \
					if [ "$$POSTGRES_HEALTH" = "healthy" ] && [ "$$RABBITMQ_HEALTH" = "healthy" ] && [ "$$MONGODB_HEALTH" = "healthy" ]; then \
						echo "$(GREEN)  ✓ All dependent services are healthy$(NC)"; \
						break; \
					fi; \
					if [ $$i -lt 10 ]; then \
						echo "$(YELLOW)  Waiting... (PostgreSQL: $$POSTGRES_HEALTH, RabbitMQ: $$RABBITMQ_HEALTH, MongoDB: $$MONGODB_HEALTH)$(NC)"; \
						sleep 3; \
					fi; \
				done; \
				if command -v docker-compose > /dev/null 2>&1; then \
					if docker-compose create --no-build webapi 2>&1; then \
						echo "$(GREEN)  ✓ WebAPI container created successfully$(NC)"; \
					else \
						if docker ps -a --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api"; then \
							echo "$(GREEN)  ✓ WebAPI container exists (was created)$(NC)"; \
						else \
							echo "$(YELLOW)  ⚠ Container creation failed. This may be due to health check dependencies.$(NC)"; \
							echo "$(YELLOW)  The container will be created automatically when you run 'make docker-up-webapi'$(NC)"; \
						fi; \
					fi; \
				elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then \
					if docker compose create --no-build webapi 2>&1; then \
						echo "$(GREEN)  ✓ WebAPI container created successfully$(NC)"; \
					else \
						if docker ps -a --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api"; then \
							echo "$(GREEN)  ✓ WebAPI container exists (was created)$(NC)"; \
						else \
							echo "$(YELLOW)  ⚠ Container creation failed. This may be due to health check dependencies.$(NC)"; \
							echo "$(YELLOW)  The container will be created automatically when you run 'make docker-up-webapi'$(NC)"; \
						fi; \
					fi; \
				fi; \
			fi; \
		else \
			BUILD_EXIT=$$?; \
			echo "$(RED)  ✗ Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
			echo "$(YELLOW)  This likely means base images are not cached.$(NC)"; \
			echo "$(YELLOW)  To cache base images (run once):$(NC)"; \
			echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"; \
			echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"; \
			echo "$(YELLOW)  Then run 'make rebuild-webapi-docker' again.$(NC)"; \
			exit 1; \
		fi; \
	else \
		echo "$(YELLOW)  ⚠ Docker not available. Skipping rebuild.$(NC)"; \
		echo "$(YELLOW)  Rebuild manually later with 'make docker-build'$(NC)"; \
		exit 1; \
	fi

rebuild-webapi-image: ## Build the webapi Docker image without ensuring services (assumes services are already running)
	@echo "$(YELLOW)Rebuilding webapi Docker image (assuming services are already running)...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(RED)  ✗ Docker is not running. Please start Docker Desktop first.$(NC)"; \
		exit 1; \
	fi
	@if docker ps -a --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api"; then \
		echo "$(YELLOW)  Stopping and removing existing webapi container...$(NC)"; \
		docker-compose stop webapi 2>/dev/null || docker compose stop webapi 2>/dev/null || true; \
		docker-compose rm -f webapi 2>/dev/null || docker compose rm -f webapi 2>/dev/null || docker rm -f boilerplate-auth-api 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building webapi image (using cached base images, only rebuilding code changes)...$(NC)"
	@echo "$(YELLOW)  Using DOCKER_BUILDKIT=0 to use legacy build (prevents pulling base images)$(NC)"
	@if DOCKER_BUILDKIT=0 docker build --pull=false -f BoilerPlate.Authentication.WebApi/Dockerfile -t boilerplate-authentication-webapi:latest . 2>&1; then \
		echo "$(GREEN)  ✓ WebAPI image built successfully$(NC)"; \
	else \
		BUILD_EXIT=$$?; \
		echo "$(RED)  ✗ Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
		echo "$(YELLOW)  This likely means base images are not cached.$(NC)"; \
		echo "$(YELLOW)  To cache base images (run once):$(NC)"; \
		echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"; \
		echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"; \
		exit 1; \
	fi

rebuild-audit-image: ## Build the audit service Docker image without ensuring services (assumes services are already running)
	@echo "$(YELLOW)Rebuilding audit service Docker image (assuming services are already running)...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(RED)  ✗ Docker is not running. Please start Docker Desktop first.$(NC)"; \
		exit 1; \
	fi
	@if docker ps -a --filter "name=boilerplate-services-audit" --format "{{.Names}}" | grep -q "boilerplate-services-audit"; then \
		echo "$(YELLOW)  Stopping and removing existing audit container...$(NC)"; \
		docker-compose stop audit 2>/dev/null || docker compose stop audit 2>/dev/null || true; \
		docker-compose rm -f audit 2>/dev/null || docker compose rm -f audit 2>/dev/null || docker rm -f boilerplate-services-audit 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building audit service image (using cached base images, only rebuilding code changes)...$(NC)"
	@echo "$(YELLOW)  Using DOCKER_BUILDKIT=0 to use legacy build (prevents pulling base images)$(NC)"
	@if DOCKER_BUILDKIT=0 docker build --pull=false -f BoilerPlate.Services.Audit/Dockerfile -t boilerplate-services-audit:latest . 2>&1; then \
		echo "$(GREEN)  ✓ Audit service image built successfully$(NC)"; \
	else \
		BUILD_EXIT=$$?; \
		echo "$(RED)  ✗ Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
		echo "$(YELLOW)  This likely means base images are not cached.$(NC)"; \
		echo "$(YELLOW)  To cache base images (run once):$(NC)"; \
		echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"; \
		echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"; \
		exit 1; \
	fi

redeploy: build-webapi-project build-audit-project build-frontend-project migrate-only rebuild-webapi-image rebuild-audit-image rebuild-frontend-docker docker-up-webapi docker-up-audit docker-up-frontend ## Rebuild code, run migrations, and redeploy webapi, audit, and frontend services (leaves third-party services unchanged)
	@echo "$(GREEN)✓ Redeploy complete!$(NC)"
	@echo ""
	@echo "$(YELLOW)Service URLs:$(NC)"
	@echo "  - Frontend: http://localhost:4200"
	@echo "  - Web API: http://localhost:8080"
	@echo "  - Swagger UI: http://localhost:8080/swagger"

rebuild-webapi: rebuild-webapi-docker ## Rebuild the webapi Docker image (alias for rebuild-webapi-docker)

rebuild-audit-docker: ensure-services ## Build the audit service Docker image and create the container (uses cached base images, only rebuilding code changes)
	@echo "$(YELLOW)Rebuilding audit service Docker image...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  ⚠ Docker is not running. Skipping image rebuild.$(NC)"; \
		echo "$(YELLOW)  Start Docker Desktop and run 'make rebuild-audit-image' to rebuild the image$(NC)"; \
		exit 1; \
	fi
	@if docker ps --filter "name=boilerplate-services-audit" --format "{{.Names}}" | grep -q "boilerplate-services-audit"; then \
		echo "$(YELLOW)  Stopping existing audit container...$(NC)"; \
		docker-compose stop audit 2>/dev/null || docker compose stop audit 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building audit service image (using cached base images, only rebuilding code changes)...$(NC)"
	@echo "$(YELLOW)  Using DOCKER_BUILDKIT=0 to use legacy build (prevents pulling base images)$(NC)"
	@echo "$(YELLOW)  If base images are not cached, this will fail. Cache them first with:$(NC)"
	@echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"
	@echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"
	@if command -v docker > /dev/null 2>&1; then \
		if DOCKER_BUILDKIT=0 docker build --pull=false -f BoilerPlate.Services.Audit/Dockerfile -t boilerplate-services-audit:latest . 2>&1; then \
			echo "$(GREEN)  ✓ Audit service image built successfully (using cached base images)$(NC)"; \
			echo "$(YELLOW)  Creating audit container (not starting it)...$(NC)"; \
			if docker ps -a --filter "name=boilerplate-services-audit" --format "{{.Names}}" | grep -q "boilerplate-services-audit"; then \
				echo "$(GREEN)  ✓ Audit container already exists$(NC)"; \
			else \
				echo "$(YELLOW)  Waiting for dependent services to be healthy (checking Docker health status)...$(NC)"; \
				for i in 1 2 3 4 5 6 7 8 9 10; do \
					RABBITMQ_HEALTH=$$(docker inspect --format='{{.State.Health.Status}}' rabbitmq-auth 2>/dev/null || echo "starting"); \
					MONGODB_HEALTH=$$(docker inspect --format='{{.State.Health.Status}}' mongodb-logs 2>/dev/null || echo "starting"); \
					if [ "$$RABBITMQ_HEALTH" = "healthy" ] && [ "$$MONGODB_HEALTH" = "healthy" ]; then \
						echo "$(GREEN)  ✓ All dependent services are healthy$(NC)"; \
						break; \
					fi; \
					if [ $$i -lt 10 ]; then \
						echo "$(YELLOW)  Waiting... (RabbitMQ: $$RABBITMQ_HEALTH, MongoDB: $$MONGODB_HEALTH)$(NC)"; \
						sleep 3; \
					fi; \
				done; \
				if command -v docker-compose > /dev/null 2>&1; then \
					if docker-compose create --no-build audit 2>&1; then \
						echo "$(GREEN)  ✓ Audit container created successfully$(NC)"; \
					else \
						if docker ps -a --filter "name=boilerplate-services-audit" --format="{{.Names}}" | grep -q "boilerplate-services-audit"; then \
							echo "$(GREEN)  ✓ Audit container exists (was created)$(NC)"; \
						else \
							echo "$(YELLOW)  ⚠ Container creation failed. This may be due to health check dependencies.$(NC)"; \
							echo "$(YELLOW)  The container will be created automatically when you run 'make docker-up-audit'$(NC)"; \
						fi; \
					fi; \
				elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then \
					if docker compose create --no-build audit 2>&1; then \
						echo "$(GREEN)  ✓ Audit container created successfully$(NC)"; \
					else \
						if docker ps -a --filter "name=boilerplate-services-audit" --format="{{.Names}}" | grep -q "boilerplate-services-audit"; then \
							echo "$(GREEN)  ✓ Audit container exists (was created)$(NC)"; \
						else \
							echo "$(YELLOW)  ⚠ Container creation failed. This may be due to health check dependencies.$(NC)"; \
							echo "$(YELLOW)  The container will be created automatically when you run 'make docker-up-audit'$(NC)"; \
						fi; \
					fi; \
				fi; \
			fi; \
		else \
			BUILD_EXIT=$$?; \
			echo "$(RED)  ✗ Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
			echo "$(YELLOW)  This likely means base images are not cached.$(NC)"; \
			echo "$(YELLOW)  To cache base images (run once):$(NC)"; \
			echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"; \
			echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"; \
			echo "$(YELLOW)  Then run 'make rebuild-audit-docker' again.$(NC)"; \
			exit 1; \
		fi; \
	else \
		echo "$(YELLOW)  ⚠ Docker not available. Skipping rebuild.$(NC)"; \
		echo "$(YELLOW)  Rebuild manually later with 'make rebuild-audit-image'$(NC)"; \
		exit 1; \
	fi

rebuild-audit: rebuild-audit-docker ## Rebuild the audit service Docker image (alias for rebuild-audit-docker)

rebuild-frontend-docker: ## Build the frontend Docker image
	@echo "$(YELLOW)Rebuilding frontend Docker image...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  ⚠ Docker is not running. Skipping image rebuild.$(NC)"; \
		echo "$(YELLOW)  Start Docker Desktop and run 'make rebuild-frontend-docker' to rebuild the image$(NC)"; \
		exit 1; \
	fi
	@if docker ps --filter "name=boilerplate-frontend" --format "{{.Names}}" | grep -q "boilerplate-frontend"; then \
		echo "$(YELLOW)  Stopping existing frontend container...$(NC)"; \
		docker-compose stop frontend 2>/dev/null || docker compose stop frontend 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building frontend image...$(NC)"
	@if command -v docker > /dev/null 2>&1; then \
		if docker build -f BoilerPlate.Frontend/Dockerfile -t boilerplate-frontend:latest . 2>&1; then \
			echo "$(GREEN)  ✓ Frontend image built successfully$(NC)"; \
		else \
			BUILD_EXIT=$$?; \
			echo "$(RED)  ✗ Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
			exit 1; \
		fi; \
	else \
		echo "$(YELLOW)  ⚠ Docker not available. Skipping rebuild.$(NC)"; \
		exit 1; \
	fi

rebuild-frontend: rebuild-frontend-docker ## Rebuild the frontend Docker image (alias for rebuild-frontend-docker)

docker-build: rebuild-webapi-docker rebuild-audit-docker rebuild-frontend-docker ## Build and start the webapi, audit, and frontend services
	@echo "$(YELLOW)Starting services...$(NC)"
	@docker-compose up -d webapi audit frontend || docker compose up -d webapi audit frontend || true
	@echo "$(GREEN)✓ Services rebuilt and started$(NC)"

docker-logs: ## View logs from all services
	@docker-compose logs -f || docker compose logs -f

docker-logs-webapi: ## View logs from webapi service only
	@docker-compose logs -f webapi || docker compose logs -f webapi

docker-logs-audit: ## View logs from audit service only
	@docker-compose logs -f audit || docker compose logs -f audit

docker-logs-frontend: ## View logs from frontend service only
	@docker-compose logs -f frontend || docker compose logs -f frontend

verify: ## Verify the setup
	@echo "$(YELLOW)Verifying setup...$(NC)"
	@echo ""
	@echo "$(YELLOW)1. Checking JWT keys:$(NC)"
	@if [ -f jwt-keys/private_key.pem ] && [ -f jwt-keys/public_key.pem ]; then \
		echo "$(GREEN)  ✓ JWT keys exist$(NC)"; \
		openssl rsa -in jwt-keys/private_key.pem -check -noout > /dev/null 2>&1 && echo "$(GREEN)  ✓ Private key is valid$(NC)" || echo "$(RED)  ✗ Private key is invalid$(NC)"; \
	else \
		echo "$(RED)  ✗ JWT keys are missing$(NC)"; \
	fi
	@echo ""
	@echo "$(YELLOW)2. Checking .env file:$(NC)"
	@if [ -f .env ]; then \
		echo "$(GREEN)  ✓ .env file exists$(NC)"; \
		grep -q "JWT_PRIVATE_KEY=" .env && echo "$(GREEN)  ✓ JWT_PRIVATE_KEY is set$(NC)" || echo "$(RED)  ✗ JWT_PRIVATE_KEY is missing$(NC)"; \
		grep -q "JWT_PUBLIC_KEY=" .env && echo "$(GREEN)  ✓ JWT_PUBLIC_KEY is set$(NC)" || echo "$(RED)  ✗ JWT_PUBLIC_KEY is missing$(NC)"; \
	else \
		echo "$(RED)  ✗ .env file is missing$(NC)"; \
	fi
	@echo ""
	@echo "$(YELLOW)3. Checking Docker services:$(NC)"
	@docker ps --filter "name=postgres-auth" --format "{{.Names}}" | grep -q "postgres-auth" && echo "$(GREEN)  ✓ PostgreSQL is running$(NC)" || echo "$(YELLOW)  ○ PostgreSQL is not running$(NC)"
	@docker ps --filter "name=rabbitmq-auth" --format "{{.Names}}" | grep -q "rabbitmq-auth" && echo "$(GREEN)  ✓ RabbitMQ is running$(NC)" || echo "$(YELLOW)  ○ RabbitMQ is not running$(NC)"
	@docker ps --filter "name=mongodb-logs" --format "{{.Names}}" | grep -q "mongodb-logs" && echo "$(GREEN)  ✓ MongoDB is running$(NC)" || echo "$(YELLOW)  ○ MongoDB is not running$(NC)"
	@docker ps --filter "name=otel-collector" --format "{{.Names}}" | grep -q "otel-collector" && echo "$(GREEN)  ✓ OTEL Collector is running$(NC)" || echo "$(YELLOW)  ○ OTEL Collector is not running$(NC)"
	@docker ps --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api" && echo "$(GREEN)  ✓ WebAPI is running$(NC)" || echo "$(YELLOW)  ○ WebAPI is not running$(NC)"
	@docker ps --filter "name=boilerplate-frontend" --format "{{.Names}}" | grep -q "boilerplate-frontend" && echo "$(GREEN)  ✓ Frontend is running$(NC)" || echo "$(YELLOW)  ○ Frontend is not running$(NC)"

clean: ## Remove generated files (keeps JWT keys)
	@echo "$(YELLOW)Cleaning generated files...$(NC)"
	@rm -f .env
	@rm -f jwt-keys/*_base64.txt
	@rm -f jwt-keys/*_single_line.txt
	@echo "$(GREEN)✓ Cleaned generated files$(NC)"
	@echo "$(YELLOW)Note: JWT key files (.pem) were preserved$(NC)"

clean-all: ## Remove all generated files including JWT keys (⚠️ WARNING)
	@echo "$(RED)⚠️  WARNING: This will delete all JWT keys!$(NC)"
	@read -p "Are you sure? [y/N] " -n 1 -r; \
	echo; \
	if [[ $$REPLY =~ ^[Yy]$$ ]]; then \
		rm -rf jwt-keys/*; \
		rm -f .env; \
		echo "$(GREEN)✓ All files cleaned$(NC)"; \
	else \
		echo "$(YELLOW)Cancelled$(NC)"; \
	fi

regen-keys: ## Regenerate JWT keys (keeps existing as backup)
	@echo "$(YELLOW)Regenerating JWT keys...$(NC)"
	@mkdir -p jwt-keys
	@if [ -f jwt-keys/private_key.pem ]; then \
		mv jwt-keys/private_key.pem jwt-keys/private_key.pem.bak; \
		mv jwt-keys/public_key.pem jwt-keys/public_key.pem.bak; \
		echo "$(YELLOW)  Backed up existing keys$(NC)"; \
	fi
	@openssl genrsa -out jwt-keys/private_key.pem 2048
	@openssl rsa -in jwt-keys/private_key.pem -pubout -out jwt-keys/public_key.pem
	@echo "$(GREEN)  ✓ New keys generated$(NC)"
	@echo "$(YELLOW)  Run 'make setup-env' to update .env file$(NC)"
