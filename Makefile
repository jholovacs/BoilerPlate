.PHONY: help setup setup-keys setup-tls-certs setup-env setup-volumes ensure-services ensure-postgres build-webapi-project build-audit-project build-event-logs-project build-diagnostics-project build-frontend-project run-migration migrate migrate-only rebuild-webapi-image rebuild-webapi-docker rebuild-audit-image rebuild-audit-docker rebuild-event-logs-image rebuild-event-logs-docker rebuild-diagnostics-image rebuild-diagnostics-docker rebuild-frontend-docker docker-up docker-up-webapi docker-up-audit docker-up-event-logs docker-up-diagnostics docker-up-frontend docker-down docker-build rebuild-webapi rebuild-audit rebuild-event-logs rebuild-diagnostics rebuild-frontend redeploy docker-logs docker-logs-webapi docker-logs-audit docker-logs-event-logs docker-logs-diagnostics docker-logs-frontend clean verify test

# Project root (avoids getcwd failures in WSL when cwd becomes invalid)
# Allow override: make setup PROJECT_ROOT=/mnt/d/Code/BoilerPlate
_PROJECT_ROOT := $(patsubst %/,%,$(dir $(abspath $(lastword $(MAKEFILE_LIST)))))
ifeq ($(strip $(_PROJECT_ROOT)),)
_PROJECT_ROOT := $(CURDIR)
endif
ifeq ($(strip $(_PROJECT_ROOT)),)
_PROJECT_ROOT := .
endif
# When getcwd fails, make may return "(unreachable)/path" - try to find a valid path
ifneq (,$(findstring (unreachable),$(_PROJECT_ROOT)))
_PROJECT_ROOT := $(shell for p in /mnt/d/Code/BoilerPlate /mnt/c/Code/BoilerPlate "$$HOME/Code/BoilerPlate" .; do [ -f "$$p/Makefile" ] 2>/dev/null && echo "$$p" && break; done)
endif
PROJECT_ROOT ?= $(_PROJECT_ROOT)
ifeq ($(strip $(PROJECT_ROOT)),)
PROJECT_ROOT := .
endif

# Detect OS (uname not on native Windows cmd - use .\redeploy.ps1 instead)
UNAME_S := $(shell uname -s 2>nul || echo Windows_NT)
OS := Unknown

ifeq ($(UNAME_S),Windows_NT)
	OS := Windows
	# /bin/bash doesn't exist on native Windows; use Git Bash (8.3 path avoids space issues)
	SHELL := C:/Progra~1/Git/bin/bash.exe
endif
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
	SHELL := C:/Progra~1/Git/bin/bash.exe
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

# ASCII-safe symbols (Unicode can display as garbled text in Git Bash)
OK := [OK]
FAIL := [FAIL]
WARN := [WARN]
NONE := [--]

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
	@if [ "$(OS)" = "Windows" ]; then \
		echo ""; \
		echo "$(YELLOW)On Windows:$(NC) Use .\redeploy.ps1 instead of make redeploy"; \
		echo "  (Makefile requires bash from WSL or Git Bash)"; \
	fi
	@echo ""
	@echo "$(YELLOW)PostgreSQL Configuration:$(NC)"
	@echo "  Host: $(POSTGRES_HOST)"
	@echo "  Port: $(POSTGRES_PORT)"
	@echo "  User: $(POSTGRES_USER)"
	@echo "  Database: $(POSTGRES_DB)"
	@echo "  (Override with: make setup POSTGRES_PASSWORD=YourPassword)"

setup: verify-prerequisites setup-keys setup-tls-certs setup-env setup-volumes ensure-services build-webapi-project build-audit-project build-event-logs-project build-diagnostics-project build-frontend-project run-migration rebuild-webapi-docker rebuild-audit-docker rebuild-event-logs-docker rebuild-diagnostics-docker rebuild-frontend-docker docker-up-webapi docker-up-audit docker-up-event-logs docker-up-diagnostics docker-up-frontend ## Complete setup: create/reset volumes, create services, build projects, run migrations, create images, and start containers
	@echo "$(GREEN)$(OK) Setup complete!$(NC)"
	@echo ""
	@echo "$(YELLOW)Service URLs (HTTPS on port 4200 - accept self-signed cert in browser):$(NC)"
	@echo "  - Frontend: https://localhost:4200"
	@echo "  - Auth API / Swagger: https://localhost:4200/swagger (proxied from webapi)"
	@echo "  - Diagnostics API / Swagger: https://localhost:4200/diagnostics/swagger"
	@echo "  - RabbitMQ Management: https://localhost:4200/amqp/ (OAuth2 - Service Administrator only)"
	@echo ""
	@echo "$(YELLOW)Next steps:$(NC)"
	@echo "  1. Review the .env file (it contains base64-encoded JWT keys)"
	@echo "  2. Access the app at https://localhost:4200 (accept the self-signed cert when prompted)"
	@echo "  3. All services are now running and ready to use!"

verify-prerequisites: ## Verify required tools are installed
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Verifying prerequisites...$(NC)"
	@cd "$(PROJECT_ROOT)" && which openssl > /dev/null 2>&1 || (echo "$(RED)$(FAIL) OpenSSL is not installed. Please install it first.$(NC)" && exit 1)
	@which docker > /dev/null 2>&1 || (echo "$(RED)$(FAIL) Docker is not installed. Please install Docker Desktop first.$(NC)" && exit 1)
	@which docker-compose > /dev/null 2>&1 || which docker compose > /dev/null 2>&1 || (echo "$(RED)$(FAIL) Docker Compose is not installed. Please install it first.$(NC)" && exit 1)
	@which dotnet > /dev/null 2>&1 || (echo "$(RED)$(FAIL) .NET SDK is not installed. Please install .NET SDK 8.0 or later.$(NC)" && exit 1)
	@cd "$(PROJECT_ROOT)" && if ! PATH="$$HOME/.dotnet/tools:$$PATH" dotnet ef --version > /dev/null 2>&1; then \
		echo "$(YELLOW)  Installing dotnet-ef tool...$(NC)"; \
		if dotnet tool install --global dotnet-ef --version 8.0.0 2>&1; then \
			echo "$(GREEN)  $(OK) dotnet-ef tool installed$(NC)"; \
		else \
			echo "$(YELLOW)  $(WARN) Failed to install dotnet-ef tool automatically.$(NC)"; \
			echo "$(YELLOW)  Please install manually: dotnet tool install --global dotnet-ef --version 8.0.0$(NC)"; \
			echo "$(YELLOW)  Then add to PATH: export PATH=\"$$HOME/.dotnet/tools:$$PATH\"$(NC)"; \
			echo "$(YELLOW)  Migrations will be skipped. Run 'make migrate' manually after installing dotnet-ef$(NC)"; \
		fi; \
	fi
	@cd "$(PROJECT_ROOT)" && if ! PATH="$$HOME/.dotnet/tools:$$PATH" dotnet ef --version > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) dotnet-ef tool is not available. Migrations will be skipped.$(NC)"; \
		echo "$(YELLOW)  Install with: dotnet tool install --global dotnet-ef --version 8.0.0$(NC)"; \
		echo "$(YELLOW)  Then add to PATH: export PATH=\"$$HOME/.dotnet/tools:$$PATH\"$(NC)"; \
	else \
		echo "$(GREEN)  $(OK) dotnet-ef tool is available$(NC)"; \
	fi
	@echo "$(GREEN)$(OK) All prerequisites are installed$(NC)"

setup-keys: ## Generate JWT keys if they don't exist
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Setting up JWT keys...$(NC)"
	@cd "$(PROJECT_ROOT)" && mkdir -p jwt-keys
	@cd "$(PROJECT_ROOT)" && if [ ! -f jwt-keys/private_key.pem ]; then \
		echo "$(YELLOW)  Generating private key...$(NC)"; \
		openssl genrsa -out jwt-keys/private_key.pem 2048; \
		echo "$(GREEN)  $(OK) Private key generated$(NC)"; \
	else \
		echo "$(GREEN)  $(OK) Private key already exists$(NC)"; \
	fi
	@cd "$(PROJECT_ROOT)" && if [ ! -f jwt-keys/public_key.pem ]; then \
		echo "$(YELLOW)  Generating public key...$(NC)"; \
		openssl rsa -in jwt-keys/private_key.pem -pubout -out jwt-keys/public_key.pem; \
		echo "$(GREEN)  $(OK) Public key generated$(NC)"; \
	else \
		echo "$(GREEN)  $(OK) Public key already exists$(NC)"; \
	fi

setup-tls-certs: ## Generate self-signed TLS cert for development (HTTPS for OAuth2/RabbitMQ)
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Setting up TLS certificates for development...$(NC)"
	@cd "$(PROJECT_ROOT)" && mkdir -p tls-certs
	@cd "$(PROJECT_ROOT)" && if [ ! -f tls-certs/ca.pem ]; then \
		echo "$(YELLOW)  Generating CA and server certificate...$(NC)"; \
		openssl genrsa -out tls-certs/ca-key.pem 2048 2>/dev/null; \
		openssl req -x509 -new -nodes -key tls-certs/ca-key.pem -sha256 -days 365 -out tls-certs/ca.pem -subj "/CN=BoilerPlate Dev CA" 2>/dev/null; \
		openssl genrsa -out tls-certs/key.pem 2048 2>/dev/null; \
		openssl req -new -key tls-certs/key.pem -out tls-certs/cert.csr -subj "/CN=localhost" -config tls-certs/openssl.cnf 2>/dev/null; \
		openssl x509 -req -in tls-certs/cert.csr -CA tls-certs/ca.pem -CAkey tls-certs/ca-key.pem -CAcreateserial -out tls-certs/cert.pem -days 365 -sha256 -extensions v3_req -extfile tls-certs/openssl.cnf 2>/dev/null; \
		rm -f tls-certs/cert.csr tls-certs/ca-key.pem tls-certs/ca.srl 2>/dev/null; \
		echo "$(GREEN)  $(OK) TLS certificates generated (replace with trusted cert in production)$(NC)"; \
	else \
		echo "$(GREEN)  $(OK) TLS certificates already exist$(NC)"; \
	fi

regen-tls-certs: ## Regenerate TLS certs (e.g. after adding SAN for frontend for RabbitMQ OAuth2)
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Regenerating TLS certificates...$(NC)"
	@cd "$(PROJECT_ROOT)" && rm -f tls-certs/ca.pem tls-certs/cert.pem tls-certs/key.pem tls-certs/ca-key.pem tls-certs/cert.csr tls-certs/ca.srl 2>/dev/null || true
	@cd "$(PROJECT_ROOT)" && "$(MAKE)" setup-tls-certs
	@echo "$(YELLOW)  Restart frontend and RabbitMQ: docker compose restart frontend rabbitmq$(NC)"

setup-volumes: ## Create or reset Docker volumes for PostgreSQL, MongoDB, RabbitMQ, and OTEL Collector
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Setting up Docker volumes...$(NC)"
	@cd "$(PROJECT_ROOT)" && if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) Docker is not running. Skipping volume setup.$(NC)"; \
		echo "$(YELLOW)  Start Docker Desktop and volumes will be created automatically when services start$(NC)"; \
		exit 0; \
	fi
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)  Stopping and removing containers that use these volumes...$(NC)"
	@cd "$(PROJECT_ROOT)" && docker stop postgres-auth mongodb-logs rabbitmq-auth otel-collector 2>/dev/null || true
	@cd "$(PROJECT_ROOT)" && docker rm -f postgres-auth mongodb-logs rabbitmq-auth otel-collector 2>/dev/null || true
	@cd "$(PROJECT_ROOT)" && if command -v docker-compose > /dev/null 2>&1; then \
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
				echo "$(GREEN)      $(OK) $$vol_full_name removed$(NC)"; \
			else \
				ERR=$$(docker volume rm $$vol_full_name 2>&1 || true); \
				if ! docker volume inspect $$vol_full_name > /dev/null 2>&1; then \
					echo "$(GREEN)      $(OK) $$vol_full_name removed$(NC)"; \
				else \
					echo "$(RED)      $(FAIL) Failed: $$ERR$(NC)"; \
				fi; \
			fi; \
		done; \
	else \
		echo "$(GREEN)    $(OK) No matching volumes found$(NC)"; \
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
				echo "$(GREEN)      $(OK) $$vol removed$(NC)"; \
			else \
				if ! docker volume inspect $$vol > /dev/null 2>&1; then \
					echo "$(GREEN)      $(OK) $$vol removed$(NC)"; \
				else \
					ERR_MSG=$$(docker volume rm $$vol 2>&1 || true); \
					echo "$(RED)      $(FAIL) Failed to remove $$vol$(NC)"; \
					echo "$(YELLOW)      Error: $$ERR_MSG$(NC)"; \
					echo "$(RED)      $(WARN) OLD DATA WILL PERSIST in $$vol$(NC)"; \
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
		echo "$(RED)$(WARN) WARNING: Some volumes still exist with old data!$(NC)"; \
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
		echo "$(GREEN)    $(OK) All volumes successfully removed$(NC)"; \
	fi
	@echo "$(YELLOW)  Creating fresh volumes...$(NC)"
	@for vol in postgres_data mongodb_data rabbitmq_data otel_collector_data; do \
		if docker volume inspect $$vol > /dev/null 2>&1; then \
			echo "$(YELLOW)    $(WARN) $$vol already exists (old data may persist)$(NC)"; \
			echo "$(YELLOW)    Skipping creation - using existing volume$(NC)"; \
		else \
			if docker volume create $$vol > /dev/null 2>&1; then \
				echo "$(GREEN)    $(OK) $$vol created$(NC)"; \
			else \
				echo "$(RED)    $(FAIL) Failed to create $$vol$(NC)"; \
			fi; \
		fi; \
	done
	@echo "$(GREEN)$(OK) Volume setup complete$(NC)"

setup-env: setup-keys ## Create .env file with base64-encoded JWT keys
	@echo "$(YELLOW)Creating .env file...$(NC)"
	@if [ ! -f jwt-keys/private_key.pem ] || [ ! -f jwt-keys/public_key.pem ]; then \
		echo "$(RED)  $(FAIL) JWT keys not found. Run 'make setup-keys' first.$(NC)"; \
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
		echo "$(RED)  $(FAIL) Neither base64 nor openssl found for encoding$(NC)"; \
		echo "$(YELLOW)  Trying PowerShell on Windows...$(NC)"; \
		if command -v powershell.exe > /dev/null 2>&1 && [ -f setup-env.ps1 ]; then \
			powershell.exe -ExecutionPolicy Bypass -File setup-env.ps1; \
		else \
			echo "$(RED)  $(FAIL) No suitable encoding tool found$(NC)"; \
			echo "$(YELLOW)  Please install OpenSSL or run setup.ps1 manually on Windows$(NC)"; \
			exit 1; \
		fi; \
	fi
	@if [ -f jwt-keys/private_key_base64.txt ] && [ -f jwt-keys/public_key_base64.txt ]; then \
		echo "JWT_PRIVATE_KEY=$$(cat jwt-keys/private_key_base64.txt | tr -d '\r')" > .env; \
		echo "JWT_PUBLIC_KEY=$$(cat jwt-keys/public_key_base64.txt | tr -d '\r')" >> .env; \
		echo "JWT_EXPIRATION_MINUTES=60" >> .env; \
		echo "" >> .env; \
		echo "# JWT Issuer URL for microservices that validate tokens (e.g. Diagnostics)." >> .env; \
		echo "# Used to fetch public key from /.well-known/jwks.json when JWT_PUBLIC_KEY is not set." >> .env; \
		echo "# Docker default: http://webapi:8080 | Local: http://localhost:8080" >> .env; \
		echo "# JWT_ISSUER_URL=http://webapi:8080" >> .env; \
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
		echo "" >> .env; \
		echo "# OAuth2 Issuer URL (HTTPS - self-signed in dev; trusted cert in production)" >> .env; \
		echo "OAUTH2_ISSUER_URL=https://host.docker.internal:4200" >> .env; \
		echo "$(GREEN)  $(OK) .env file created with all connection strings$(NC)"; \
	else \
		echo "$(RED)  $(FAIL) Failed to create base64 encoded keys$(NC)"; \
		exit 1; \
	fi

ensure-services: ## Ensure all required services are running (PostgreSQL, RabbitMQ, MongoDB, OTEL Collector, but NOT webapi)
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Ensuring required services are running...$(NC)"
	@cd "$(PROJECT_ROOT)" && if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) Docker is not running. Please start Docker Desktop first.$(NC)"; \
		exit 1; \
	fi
	@# Ensure PostgreSQL is running
	@cd "$(or $(strip $(PROJECT_ROOT)),.)" && "$(MAKE)" ensure-postgres
	@# Ensure RabbitMQ is running
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Ensuring RabbitMQ is running...$(NC)"
	@cd "$(PROJECT_ROOT)" && if docker ps --filter "name=rabbitmq-auth" --format "{{.Names}}" | grep -q "rabbitmq-auth"; then \
		echo "$(GREEN)  $(OK) RabbitMQ container is already running$(NC)"; \
	elif docker ps -a --filter "name=rabbitmq-auth" --format "{{.Names}}" | grep -q "rabbitmq-auth"; then \
		echo "$(YELLOW)  Starting existing RabbitMQ container...$(NC)"; \
		docker-compose start rabbitmq > /dev/null 2>&1 || docker compose start rabbitmq > /dev/null 2>&1 || docker start rabbitmq-auth > /dev/null 2>&1 || true; \
		echo "$(GREEN)  $(OK) RabbitMQ started$(NC)"; \
	else \
		echo "$(YELLOW)  Creating and starting RabbitMQ container...$(NC)"; \
		docker-compose up -d rabbitmq > /dev/null 2>&1 || docker compose up -d rabbitmq > /dev/null 2>&1 || { \
			echo "$(YELLOW)  $(WARN) Could not start RabbitMQ automatically.$(NC)"; \
			exit 1; \
		}; \
		echo "$(GREEN)  $(OK) RabbitMQ started$(NC)"; \
	fi
	@# Ensure MongoDB is running
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Ensuring MongoDB is running...$(NC)"
	@cd "$(PROJECT_ROOT)" && if docker ps --filter "name=mongodb-logs" --format "{{.Names}}" | grep -q "mongodb-logs"; then \
		echo "$(GREEN)  $(OK) MongoDB container is already running$(NC)"; \
	elif docker ps -a --filter "name=mongodb-logs" --format "{{.Names}}" | grep -q "mongodb-logs"; then \
		echo "$(YELLOW)  Starting existing MongoDB container...$(NC)"; \
		docker-compose start mongodb > /dev/null 2>&1 || docker compose start mongodb > /dev/null 2>&1 || docker start mongodb-logs > /dev/null 2>&1 || true; \
		echo "$(GREEN)  $(OK) MongoDB started$(NC)"; \
	else \
		echo "$(YELLOW)  Creating and starting MongoDB container...$(NC)"; \
		docker-compose up -d mongodb > /dev/null 2>&1 || docker compose up -d mongodb > /dev/null 2>&1 || { \
			echo "$(YELLOW)  $(WARN) Could not start MongoDB automatically.$(NC)"; \
			exit 1; \
		}; \
		echo "$(GREEN)  $(OK) MongoDB started$(NC)"; \
	fi
	@# Ensure OTEL Collector is running
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Ensuring OTEL Collector is running...$(NC)"
	@cd "$(PROJECT_ROOT)" && if docker ps --filter "name=otel-collector" --format "{{.Names}}" | grep -q "otel-collector"; then \
		echo "$(GREEN)  $(OK) OTEL Collector container is already running$(NC)"; \
	elif docker ps -a --filter "name=otel-collector" --format "{{.Names}}" | grep -q "otel-collector"; then \
		echo "$(YELLOW)  Starting existing OTEL Collector container...$(NC)"; \
		docker-compose start otel-collector > /dev/null 2>&1 || docker compose start otel-collector > /dev/null 2>&1 || docker start otel-collector > /dev/null 2>&1 || true; \
		echo "$(GREEN)  $(OK) OTEL Collector started$(NC)"; \
	else \
		echo "$(YELLOW)  Creating and starting OTEL Collector container...$(NC)"; \
		if ! docker image inspect otel/opentelemetry-collector-contrib:latest > /dev/null 2>&1; then \
			echo "$(YELLOW)  Pulling OTEL Collector image...$(NC)"; \
			docker pull otel/opentelemetry-collector-contrib:latest > /dev/null 2>&1 || { \
				echo "$(RED)  $(FAIL) Failed to pull OTEL Collector image. Check your internet connection.$(NC)"; \
				exit 1; \
			}; \
		fi; \
		docker-compose up -d otel-collector > /dev/null 2>&1 || docker compose up -d otel-collector > /dev/null 2>&1 || { \
			echo "$(YELLOW)  $(WARN) Could not start OTEL Collector automatically.$(NC)"; \
			exit 1; \
		}; \
		echo "$(GREEN)  $(OK) OTEL Collector started$(NC)"; \
	fi
	@echo "$(GREEN)$(OK) All required services are running$(NC)"

build-webapi-project: ## Build the webapi .NET project (dotnet build)
	@echo "$(YELLOW)Building webapi .NET project...$(NC)"
	@if ! command -v dotnet > /dev/null 2>&1; then \
		echo "$(RED)  $(FAIL) .NET SDK is not installed. Please install .NET SDK 8.0 or later.$(NC)"; \
		exit 1; \
	fi
	@if cd "$(PROJECT_ROOT)" && dotnet build BoilerPlate.Authentication.WebApi/BoilerPlate.Authentication.WebApi.csproj -c Release -m 2>&1; then \
		echo "$(GREEN)  $(OK) WebAPI project built successfully$(NC)"; \
	else \
		echo "$(RED)  $(FAIL) WebAPI project build failed$(NC)"; \
		exit 1; \
	fi

build-audit-project: ## Build the audit service .NET project (dotnet build)
	@echo "$(YELLOW)Building audit service .NET project...$(NC)"
	@if ! command -v dotnet > /dev/null 2>&1; then \
		echo "$(RED)  $(FAIL) .NET SDK is not installed. Please install .NET SDK 8.0 or later.$(NC)"; \
		exit 1; \
	fi
	@if cd "$(PROJECT_ROOT)" && dotnet build BoilerPlate.Services.Audit/BoilerPlate.Services.Audit.csproj -c Release -m 2>&1; then \
		echo "$(GREEN)  $(OK) Audit service project built successfully$(NC)"; \
	else \
		echo "$(RED)  $(FAIL) Audit service project build failed$(NC)"; \
		exit 1; \
	fi

build-event-logs-project: ## Build the event logs service .NET project (dotnet build)
	@echo "$(YELLOW)Building event logs service .NET project...$(NC)"
	@if ! command -v dotnet > /dev/null 2>&1; then \
		echo "$(RED)  $(FAIL) .NET SDK is not installed. Please install .NET SDK 8.0 or later.$(NC)"; \
		exit 1; \
	fi
	@if cd "$(PROJECT_ROOT)" && dotnet build BoilerPlate.Services.EventLogs/BoilerPlate.Services.EventLogs.csproj -c Release -m 2>&1; then \
		echo "$(GREEN)  $(OK) Event logs service project built successfully$(NC)"; \
	else \
		echo "$(RED)  $(FAIL) Event logs service project build failed$(NC)"; \
		exit 1; \
	fi

build-diagnostics-project: ## Build the diagnostics API .NET project (dotnet build)
	@echo "$(YELLOW)Building diagnostics API .NET project...$(NC)"
	@if ! command -v dotnet > /dev/null 2>&1; then \
		echo "$(RED)  $(FAIL) .NET SDK is not installed. Please install .NET SDK 8.0 or later.$(NC)"; \
		exit 1; \
	fi
	@if cd "$(PROJECT_ROOT)" && dotnet build BoilerPlate.Diagnostics.WebApi/BoilerPlate.Diagnostics.WebApi.csproj -c Release -m 2>&1; then \
		echo "$(GREEN)  $(OK) Diagnostics API project built successfully$(NC)"; \
	else \
		echo "$(RED)  $(FAIL) Diagnostics API project build failed$(NC)"; \
		exit 1; \
	fi

build-frontend-project: ## Build the frontend Angular project (npm install and build)
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Building frontend Angular project...$(NC)"
	@cd "$(PROJECT_ROOT)" && if ! command -v node > /dev/null 2>&1; then \
		echo "$(RED)  $(FAIL) Node.js is not installed. Please install Node.js 18+ first.$(NC)"; \
		echo "$(YELLOW)  Frontend build will be skipped. Docker build will handle it.$(NC)"; \
		exit 0; \
	fi
	@cd "$(PROJECT_ROOT)" && if ! command -v npm > /dev/null 2>&1; then \
		echo "$(RED)  $(FAIL) npm is not installed. Please install npm first.$(NC)"; \
		echo "$(YELLOW)  Frontend build will be skipped. Docker build will handle it.$(NC)"; \
		exit 0; \
	fi
	@cd "$(PROJECT_ROOT)" && if [ ! -d "BoilerPlate.Frontend" ]; then \
		echo "$(YELLOW)  $(WARN) Frontend directory not found. Skipping frontend build.$(NC)"; \
		exit 0; \
	fi
	@cd "$(PROJECT_ROOT)/BoilerPlate.Frontend" && \
	if [ ! -d "node_modules" ]; then \
		echo "$(YELLOW)  Installing npm dependencies...$(NC)"; \
		npm install 2>&1 || { \
			echo "$(YELLOW)  $(WARN) npm install failed. Docker build will handle dependencies.$(NC)"; \
			exit 0; \
		}; \
	fi && \
	echo "$(YELLOW)  Building Angular application...$(NC)" && \
	NODE_OPTIONS="--max-old-space-size=4096" npm run build 2>&1 || { \
		echo "$(YELLOW)  $(WARN) npm build failed. Docker build will handle it.$(NC)"; \
		exit 0; \
	} && \
	echo "$(GREEN)  $(OK) Frontend project built successfully$(NC)"

ensure-postgres: ## Ensure PostgreSQL service is running (starts it if needed)
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Ensuring PostgreSQL is running...$(NC)"
	@cd "$(PROJECT_ROOT)" && if docker ps --filter "name=postgres-auth" --format "{{.Names}}" | grep -q "postgres-auth"; then \
		echo "$(GREEN)  $(OK) PostgreSQL container is already running$(NC)"; \
		if PGPASSWORD="$(POSTGRES_PASSWORD)" docker exec -e PGPASSWORD="$(POSTGRES_PASSWORD)" postgres-auth pg_isready -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
			echo "$(GREEN)  $(OK) PostgreSQL is ready to accept connections$(NC)"; \
		else \
			echo "$(YELLOW)  Waiting for PostgreSQL to be ready...$(NC)"; \
			for i in 1 2 3 4 5; do \
				if PGPASSWORD="$(POSTGRES_PASSWORD)" docker exec -e PGPASSWORD="$(POSTGRES_PASSWORD)" postgres-auth pg_isready -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
					echo "$(GREEN)  $(OK) PostgreSQL is ready$(NC)"; \
					break; \
				fi; \
				if [ $$i -lt 5 ]; then \
					echo "$(YELLOW)  Waiting... (attempt $$i/5)$(NC)"; \
					sleep 2; \
				fi; \
			done; \
		fi; \
	elif command -v pg_isready > /dev/null 2>&1 && PGPASSWORD="$(POSTGRES_PASSWORD)" pg_isready -h "$(POSTGRES_HOST)" -p "$(POSTGRES_PORT)" -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
		echo "$(GREEN)  $(OK) PostgreSQL is accessible locally (not in Docker)$(NC)"; \
	else \
		echo "$(YELLOW)  PostgreSQL is not running. Starting PostgreSQL container...$(NC)"; \
		if docker ps -a --filter "name=postgres-auth" --format "{{.Names}}" | grep -q "postgres-auth"; then \
			echo "$(YELLOW)  Starting existing PostgreSQL container...$(NC)"; \
			docker start postgres-auth > /dev/null 2>&1 || docker-compose start postgres > /dev/null 2>&1 || docker compose start postgres > /dev/null 2>&1 || true; \
		else \
			echo "$(YELLOW)  Creating and starting PostgreSQL container...$(NC)"; \
			docker-compose up -d postgres > /dev/null 2>&1 || docker compose up -d postgres > /dev/null 2>&1 || { \
				echo "$(YELLOW)  $(WARN) Could not start PostgreSQL automatically.$(NC)"; \
				echo "$(YELLOW)  Please run 'make docker-up' or 'docker-compose up -d postgres' manually$(NC)"; \
				exit 0; \
			}; \
		fi; \
		echo "$(YELLOW)  Waiting for PostgreSQL to be ready (this may take 10-20 seconds)...$(NC)"; \
		for i in 1 2 3 4 5 6 7 8 9 10; do \
			if PGPASSWORD="$(POSTGRES_PASSWORD)" docker exec -e PGPASSWORD="$(POSTGRES_PASSWORD)" postgres-auth pg_isready -U "$(POSTGRES_USER)" > /dev/null 2>&1 || PGPASSWORD="$(POSTGRES_PASSWORD)" pg_isready -h "$(POSTGRES_HOST)" -p "$(POSTGRES_PORT)" -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
				echo "$(GREEN)  $(OK) PostgreSQL is ready$(NC)"; \
				break; \
			fi; \
			if [ $$i -lt 10 ]; then \
				echo "$(YELLOW)  Waiting for PostgreSQL... (attempt $$i/10)$(NC)"; \
				sleep 2; \
			fi; \
			if [ $$i -eq 10 ]; then \
				echo "$(YELLOW)  $(WARN) PostgreSQL did not become ready in time.$(NC)"; \
				echo "$(YELLOW)  Migrations may fail. Run 'make migrate' manually once PostgreSQL is ready$(NC)"; \
			fi; \
		done; \
	fi

run-migration: ensure-services ## Run database migrations (requires PostgreSQL to be running and ready)
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Running database migrations...$(NC)"
	@cd "$(PROJECT_ROOT)" && if [ ! -f .env ]; then \
		echo "$(RED)  $(FAIL) .env file not found. Run 'make setup-env' first.$(NC)"; \
		exit 1; \
	fi
	@cd "$(PROJECT_ROOT)" && if ! PATH="$$HOME/.dotnet/tools:$$PATH" dotnet ef --version > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) dotnet-ef tool is not available. Skipping migration.$(NC)"; \
		echo "$(YELLOW)  Install with: dotnet tool install --global dotnet-ef --version 8.0.0$(NC)"; \
		echo "$(YELLOW)  Then add to PATH: export PATH=\"$$HOME/.dotnet/tools:$$PATH\"$(NC)"; \
		echo "$(YELLOW)  Run 'make migrate' manually after fixing the dotnet-ef tool$(NC)"; \
		exit 0; \
	fi
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)  Verifying PostgreSQL is ready for connections...$(NC)"
	@cd "$(PROJECT_ROOT)" && if ! PGPASSWORD="$(POSTGRES_PASSWORD)" docker exec -e PGPASSWORD="$(POSTGRES_PASSWORD)" postgres-auth pg_isready -U "$(POSTGRES_USER)" > /dev/null 2>&1 && ! PGPASSWORD="$(POSTGRES_PASSWORD)" pg_isready -h "$(POSTGRES_HOST)" -p "$(POSTGRES_PORT)" -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) PostgreSQL is not ready. Waiting a bit longer...$(NC)"; \
		sleep 3; \
		if ! PGPASSWORD="$(POSTGRES_PASSWORD)" docker exec -e PGPASSWORD="$(POSTGRES_PASSWORD)" postgres-auth pg_isready -U "$(POSTGRES_USER)" > /dev/null 2>&1 && ! PGPASSWORD="$(POSTGRES_PASSWORD)" pg_isready -h "$(POSTGRES_HOST)" -p "$(POSTGRES_PORT)" -U "$(POSTGRES_USER)" > /dev/null 2>&1; then \
			echo "$(YELLOW)  $(WARN) PostgreSQL is still not ready. Skipping migrations.$(NC)"; \
			echo "$(YELLOW)  Run 'make migrate' manually once PostgreSQL is ready$(NC)"; \
			exit 0; \
		fi; \
	fi
	@echo "$(GREEN)  $(OK) PostgreSQL is ready for migrations$(NC)"
	@echo "$(YELLOW)  Applying migrations with credentials...$(NC)"
	@echo "$(YELLOW)  Connection: Host=$(POSTGRES_HOST), Port=$(POSTGRES_PORT), Database=$(POSTGRES_DB), User=$(POSTGRES_USER)$(NC)"
	@cd "$(PROJECT_ROOT)" && if PATH="$$HOME/.dotnet/tools:$$PATH" PGPASSWORD="$(POSTGRES_PASSWORD)" ConnectionStrings__PostgreSqlConnection="Host=$(POSTGRES_HOST);Port=$(POSTGRES_PORT);Database=$(POSTGRES_DB);Username=$(POSTGRES_USER);Password=$(POSTGRES_PASSWORD)" dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext 2>&1; then \
		echo "$(GREEN)  $(OK) Migrations applied successfully$(NC)"; \
	else \
		MIGRATION_EXIT=$$?; \
		echo ""; \
		echo "$(YELLOW)  $(WARN) Migration failed (exit code: $$MIGRATION_EXIT)$(NC)"; \
		echo "$(YELLOW)  Check PostgreSQL connection and credentials$(NC)"; \
		echo "$(YELLOW)  Override credentials: make migrate POSTGRES_PASSWORD=YourPassword$(NC)"; \
		exit 0; \
	fi

migrate: ## Apply database migrations (can be run independently)
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Applying database migrations...$(NC)"
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)  Connection: Host=$(POSTGRES_HOST), Port=$(POSTGRES_PORT), Database=$(POSTGRES_DB), User=$(POSTGRES_USER)$(NC)"
	@cd "$(PROJECT_ROOT)" && PATH="$$HOME/.dotnet/tools:$$PATH" PGPASSWORD="$(POSTGRES_PASSWORD)" ConnectionStrings__PostgreSqlConnection="Host=$(POSTGRES_HOST);Port=$(POSTGRES_PORT);Database=$(POSTGRES_DB);Username=$(POSTGRES_USER);Password=$(POSTGRES_PASSWORD)" dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext 2>&1 | sed '/MONGODB_CONNECTION_STRING/d' || true
	@echo "$(GREEN)$(OK) Migration command completed$(NC)"

migrate-only: ## Apply database migrations without ensuring services (assumes PostgreSQL is already running)
	@echo "$(YELLOW)Applying database migrations (assuming PostgreSQL is already running)...$(NC)"
	@cd "$(PROJECT_ROOT)" && if [ ! -f .env ]; then \
		echo "$(RED)  $(FAIL) .env file not found. Run 'make setup-env' first.$(NC)"; \
		exit 1; \
	fi
	@cd "$(PROJECT_ROOT)" && if ! PATH="$$HOME/.dotnet/tools:$$PATH" dotnet ef --version > /dev/null 2>&1; then \
		echo "$(YELLOW)  Installing dotnet-ef tool...$(NC)"; \
		if dotnet tool install --global dotnet-ef --version 8.0.0 2>&1; then \
			echo "$(GREEN)  $(OK) dotnet-ef tool installed$(NC)"; \
		else \
			echo "$(YELLOW)  $(WARN) dotnet-ef tool is not available. Skipping migration.$(NC)"; \
			echo "$(YELLOW)  Install with: dotnet tool install --global dotnet-ef --version 8.0.0$(NC)"; \
			echo "$(YELLOW)  Run 'make migrate' manually after installing$(NC)"; \
			exit 0; \
		fi; \
	fi
	@cd "$(PROJECT_ROOT)" && if ! PATH="$$HOME/.dotnet/tools:$$PATH" dotnet ef --version > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) dotnet-ef tool is not available. Skipping migration.$(NC)"; \
		echo "$(YELLOW)  Run 'make migrate' manually after installing dotnet-ef$(NC)"; \
		exit 0; \
	fi
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)  Connection: Host=$(POSTGRES_HOST), Port=$(POSTGRES_PORT), Database=$(POSTGRES_DB), User=$(POSTGRES_USER)$(NC)"
	@cd "$(PROJECT_ROOT)" && if PATH="$$HOME/.dotnet/tools:$$PATH" PGPASSWORD="$(POSTGRES_PASSWORD)" ConnectionStrings__PostgreSqlConnection="Host=$(POSTGRES_HOST);Port=$(POSTGRES_PORT);Database=$(POSTGRES_DB);Username=$(POSTGRES_USER);Password=$(POSTGRES_PASSWORD)" dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext 2>&1 | sed '/MONGODB_CONNECTION_STRING/d'; then \
		echo "$(GREEN)  $(OK) Migrations applied successfully$(NC)"; \
	else \
		MIGRATION_EXIT=$$?; \
		echo "$(RED)  $(FAIL) Migration failed (exit code: $$MIGRATION_EXIT)$(NC)"; \
		echo "$(YELLOW)  Make sure PostgreSQL is running and accessible$(NC)"; \
		echo "$(YELLOW)  Override credentials: make migrate-only POSTGRES_PASSWORD=YourPassword$(NC)"; \
		exit 1; \
	fi

docker-up: ## Start all Docker services
	@echo "$(YELLOW)Starting Docker services...$(NC)"
	@docker-compose up -d || docker compose up -d
	@echo "$(GREEN)$(OK) Services started$(NC)"
	@echo "$(YELLOW)Waiting for services to be healthy...$(NC)"
	@sleep 5
	@echo "$(GREEN)$(OK) Services are running$(NC)"

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
		echo "$(RED)  $(FAIL) Docker Compose is not available$(NC)"; \
		exit 1; \
	fi
	@echo "$(GREEN)$(OK) WebAPI service started$(NC)"

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
		echo "$(RED)  $(FAIL) Docker Compose is not available$(NC)"; \
		exit 1; \
	fi
	@echo "$(GREEN)$(OK) Audit service started$(NC)"

docker-up-diagnostics: ## Start only the diagnostics API service (assumes other services are already running)
	@echo "$(YELLOW)Starting diagnostics API service...$(NC)"
	@if command -v docker-compose > /dev/null 2>&1; then \
		docker-compose up -d --no-deps diagnostics 2>&1 || { \
			echo "$(YELLOW)  Falling back to starting without --no-deps...$(NC)"; \
			docker-compose up -d diagnostics 2>&1; \
		}; \
	elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then \
		docker compose up -d --no-deps diagnostics 2>&1 || { \
			echo "$(YELLOW)  Falling back to starting without --no-deps...$(NC)"; \
			docker compose up -d diagnostics 2>&1; \
		}; \
	else \
		echo "$(RED)  $(FAIL) Docker Compose is not available$(NC)"; \
		exit 1; \
	fi
	@echo "$(GREEN)$(OK) Diagnostics API service started$(NC)"

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
		echo "$(RED)  $(FAIL) Docker Compose is not available$(NC)"; \
		exit 1; \
	fi
	@echo "$(GREEN)$(OK) Frontend service started$(NC)"

docker-down: ## Stop all Docker services
	@echo "$(YELLOW)Stopping Docker services...$(NC)"
	@docker-compose down || docker compose down
	@echo "$(GREEN)$(OK) Services stopped$(NC)"

rebuild-webapi-docker: ensure-services ## Build the webapi Docker image and create the container (uses cached base images, only rebuilding code changes)
	@echo "$(YELLOW)Rebuilding webapi Docker image...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) Docker is not running. Skipping image rebuild.$(NC)"; \
		echo "$(YELLOW)  Start Docker Desktop and run 'make docker-build' to rebuild the image$(NC)"; \
		exit 1; \
	fi
	@if docker ps --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api"; then \
		echo "$(YELLOW)  Stopping existing webapi container...$(NC)"; \
		docker-compose stop webapi 2>/dev/null || docker compose stop webapi 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building webapi image (using cached base images, only rebuilding code changes)...$(NC)"
	@echo "$(YELLOW)  Using DOCKER_BUILDKIT=1 for parallel builds and better caching$(NC)"
	@echo "$(YELLOW)  If base images are not cached, this will fail. Cache them first with:$(NC)"
	@echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"
	@echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"
	@if command -v docker > /dev/null 2>&1; then \
		if DOCKER_BUILDKIT=1 docker build --pull=false -f BoilerPlate.Authentication.WebApi/Dockerfile -t boilerplate-authentication-webapi:latest . 2>&1; then \
			echo "$(GREEN)  $(OK) WebAPI image built successfully (using cached base images)$(NC)"; \
			echo "$(YELLOW)  Creating webapi container (not starting it)...$(NC)"; \
			if docker ps -a --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api"; then \
				echo "$(GREEN)  $(OK) WebAPI container already exists$(NC)"; \
			else \
				echo "$(YELLOW)  Waiting for dependent services to be healthy (checking Docker health status)...$(NC)"; \
				for i in 1 2 3 4 5 6 7 8 9 10; do \
					POSTGRES_HEALTH=$$(docker inspect --format='{{.State.Health.Status}}' postgres-auth 2>/dev/null || echo "starting"); \
					RABBITMQ_HEALTH=$$(docker inspect --format='{{.State.Health.Status}}' rabbitmq-auth 2>/dev/null || echo "starting"); \
					MONGODB_HEALTH=$$(docker inspect --format='{{.State.Health.Status}}' mongodb-logs 2>/dev/null || echo "starting"); \
					if [ "$$POSTGRES_HEALTH" = "healthy" ] && [ "$$RABBITMQ_HEALTH" = "healthy" ] && [ "$$MONGODB_HEALTH" = "healthy" ]; then \
						echo "$(GREEN)  $(OK) All dependent services are healthy$(NC)"; \
						break; \
					fi; \
					if [ $$i -lt 10 ]; then \
						echo "$(YELLOW)  Waiting... (PostgreSQL: $$POSTGRES_HEALTH, RabbitMQ: $$RABBITMQ_HEALTH, MongoDB: $$MONGODB_HEALTH)$(NC)"; \
						sleep 3; \
					fi; \
				done; \
				if command -v docker-compose > /dev/null 2>&1; then \
					if docker-compose create --no-build webapi 2>&1; then \
						echo "$(GREEN)  $(OK) WebAPI container created successfully$(NC)"; \
					else \
						if docker ps -a --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api"; then \
							echo "$(GREEN)  $(OK) WebAPI container exists (was created)$(NC)"; \
						else \
							echo "$(YELLOW)  $(WARN) Container creation failed. This may be due to health check dependencies.$(NC)"; \
							echo "$(YELLOW)  The container will be created automatically when you run 'make docker-up-webapi'$(NC)"; \
						fi; \
					fi; \
				elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then \
					if docker compose create --no-build webapi 2>&1; then \
						echo "$(GREEN)  $(OK) WebAPI container created successfully$(NC)"; \
					else \
						if docker ps -a --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api"; then \
							echo "$(GREEN)  $(OK) WebAPI container exists (was created)$(NC)"; \
						else \
							echo "$(YELLOW)  $(WARN) Container creation failed. This may be due to health check dependencies.$(NC)"; \
							echo "$(YELLOW)  The container will be created automatically when you run 'make docker-up-webapi'$(NC)"; \
						fi; \
					fi; \
				fi; \
			fi; \
		else \
			BUILD_EXIT=$$?; \
			echo "$(RED)  $(FAIL) Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
			echo "$(YELLOW)  This likely means base images are not cached.$(NC)"; \
			echo "$(YELLOW)  To cache base images (run once):$(NC)"; \
			echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"; \
			echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"; \
			echo "$(YELLOW)  Then run 'make rebuild-webapi-docker' again.$(NC)"; \
			exit 1; \
		fi; \
	else \
		echo "$(YELLOW)  $(WARN) Docker not available. Skipping rebuild.$(NC)"; \
		echo "$(YELLOW)  Rebuild manually later with 'make docker-build'$(NC)"; \
		exit 1; \
	fi

rebuild-webapi-image: ## Build the webapi Docker image without ensuring services (assumes services are already running)
	@echo "$(YELLOW)Rebuilding webapi Docker image (assuming services are already running)...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(RED)  $(FAIL) Docker is not running. Please start Docker Desktop first.$(NC)"; \
		exit 1; \
	fi
	@if docker ps -a --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api"; then \
		echo "$(YELLOW)  Stopping and removing existing webapi container...$(NC)"; \
		docker-compose stop webapi 2>/dev/null || docker compose stop webapi 2>/dev/null || true; \
		docker-compose rm -f webapi 2>/dev/null || docker compose rm -f webapi 2>/dev/null || docker rm -f boilerplate-auth-api 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building webapi image (using cached base images, only rebuilding code changes)...$(NC)"
	@echo "$(YELLOW)  Using DOCKER_BUILDKIT=1 for parallel builds and better caching$(NC)"
	@if DOCKER_BUILDKIT=1 docker build --pull=false -f BoilerPlate.Authentication.WebApi/Dockerfile -t boilerplate-authentication-webapi:latest . 2>&1; then \
		echo "$(GREEN)  $(OK) WebAPI image built successfully$(NC)"; \
	else \
		BUILD_EXIT=$$?; \
		echo "$(RED)  $(FAIL) Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
		echo "$(YELLOW)  This likely means base images are not cached.$(NC)"; \
		echo "$(YELLOW)  To cache base images (run once):$(NC)"; \
		echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"; \
		echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"; \
		exit 1; \
	fi

rebuild-audit-image: ## Build the audit service Docker image without ensuring services (assumes services are already running)
	@echo "$(YELLOW)Rebuilding audit service Docker image (assuming services are already running)...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(RED)  $(FAIL) Docker is not running. Please start Docker Desktop first.$(NC)"; \
		exit 1; \
	fi
	@if docker ps -a --filter "name=boilerplate-services-audit" --format "{{.Names}}" | grep -q "boilerplate-services-audit"; then \
		echo "$(YELLOW)  Stopping and removing existing audit container...$(NC)"; \
		docker-compose stop audit 2>/dev/null || docker compose stop audit 2>/dev/null || true; \
		docker-compose rm -f audit 2>/dev/null || docker compose rm -f audit 2>/dev/null || docker rm -f boilerplate-services-audit 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building audit service image (using cached base images, only rebuilding code changes)...$(NC)"
	@echo "$(YELLOW)  Using DOCKER_BUILDKIT=1 for parallel builds and better caching$(NC)"
	@if DOCKER_BUILDKIT=1 docker build --pull=false -f BoilerPlate.Services.Audit/Dockerfile -t boilerplate-services-audit:latest . 2>&1; then \
		echo "$(GREEN)  $(OK) Audit service image built successfully$(NC)"; \
	else \
		BUILD_EXIT=$$?; \
		echo "$(RED)  $(FAIL) Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
		echo "$(YELLOW)  This likely means base images are not cached.$(NC)"; \
		echo "$(YELLOW)  To cache base images (run once):$(NC)"; \
		echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"; \
		echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"; \
		exit 1; \
	fi

rebuild-diagnostics-image: ## Build the diagnostics API Docker image without ensuring services (assumes services are already running)
	@echo "$(YELLOW)Rebuilding diagnostics API Docker image (assuming services are already running)...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(RED)  $(FAIL) Docker is not running. Please start Docker Desktop first.$(NC)"; \
		exit 1; \
	fi
	@if docker ps -a --filter "name=boilerplate-diagnostics-api" --format "{{.Names}}" | grep -q "boilerplate-diagnostics-api"; then \
		echo "$(YELLOW)  Stopping and removing existing diagnostics API container...$(NC)"; \
		docker-compose stop diagnostics 2>/dev/null || docker compose stop diagnostics 2>/dev/null || true; \
		docker-compose rm -f diagnostics 2>/dev/null || docker compose rm -f diagnostics 2>/dev/null || docker rm -f boilerplate-diagnostics-api 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building diagnostics API image (using cached base images, only rebuilding code changes)...$(NC)"
	@echo "$(YELLOW)  Using DOCKER_BUILDKIT=1 for parallel builds and better caching$(NC)"
	@if DOCKER_BUILDKIT=1 docker build --pull=false -f BoilerPlate.Diagnostics.WebApi/Dockerfile -t boilerplate-diagnostics-webapi:latest . 2>&1; then \
		echo "$(GREEN)  $(OK) Diagnostics API image built successfully$(NC)"; \
	else \
		BUILD_EXIT=$$?; \
		echo "$(RED)  $(FAIL) Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
		echo "$(YELLOW)  This likely means base images are not cached.$(NC)"; \
		echo "$(YELLOW)  To cache base images (run once):$(NC)"; \
		echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"; \
		echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"; \
		exit 1; \
	fi

rebuild-diagnostics-docker: ensure-services ## Build the diagnostics API Docker image and create the container (uses cached base images, only rebuilding code changes)
	@echo "$(YELLOW)Rebuilding diagnostics API Docker image...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) Docker is not running. Skipping image rebuild.$(NC)"; \
		echo "$(YELLOW)  Start Docker Desktop and run 'make rebuild-diagnostics-image' to rebuild the image$(NC)"; \
		exit 1; \
	fi
	@if docker ps --filter "name=boilerplate-diagnostics-api" --format "{{.Names}}" | grep -q "boilerplate-diagnostics-api"; then \
		echo "$(YELLOW)  Stopping existing diagnostics API container...$(NC)"; \
		docker-compose stop diagnostics 2>/dev/null || docker compose stop diagnostics 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building diagnostics API image (using cached base images, only rebuilding code changes)...$(NC)"
	@echo "$(YELLOW)  Using DOCKER_BUILDKIT=1 for parallel builds and better caching$(NC)"
	@if command -v docker > /dev/null 2>&1; then \
		if DOCKER_BUILDKIT=1 docker build --pull=false -f BoilerPlate.Diagnostics.WebApi/Dockerfile -t boilerplate-diagnostics-webapi:latest . 2>&1; then \
			echo "$(GREEN)  $(OK) Diagnostics API image built successfully (using cached base images)$(NC)"; \
			echo "$(YELLOW)  Creating diagnostics API container (not starting it)...$(NC)"; \
			if docker ps -a --filter "name=boilerplate-diagnostics-api" --format "{{.Names}}" | grep -q "boilerplate-diagnostics-api"; then \
				echo "$(GREEN)  $(OK) Diagnostics API container already exists$(NC)"; \
			else \
				if command -v docker-compose > /dev/null 2>&1; then \
					docker-compose create --no-build diagnostics 2>&1 || true; \
				elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then \
					docker compose create --no-build diagnostics 2>&1 || true; \
				fi; \
			fi; \
		else \
			BUILD_EXIT=$$?; \
			echo "$(RED)  $(FAIL) Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
			echo "$(YELLOW)  To cache base images (run once):$(NC)"; \
			echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"; \
			echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"; \
			exit 1; \
		fi; \
	else \
		echo "$(YELLOW)  $(WARN) Docker not available. Skipping rebuild.$(NC)"; \
		exit 1; \
	fi

rebuild-diagnostics: rebuild-diagnostics-docker ## Rebuild the diagnostics API Docker image (alias for rebuild-diagnostics-docker)

redeploy: build-webapi-project build-audit-project build-event-logs-project build-diagnostics-project build-frontend-project migrate-only rebuild-webapi-image rebuild-audit-image rebuild-event-logs-image rebuild-diagnostics-image rebuild-frontend-docker docker-up-webapi docker-up-audit docker-up-event-logs docker-up-diagnostics docker-up-frontend ## Rebuild code, run migrations, and redeploy webapi, audit, event-logs, diagnostics, and frontend services (leaves third-party services unchanged)
	@echo "$(GREEN)$(OK) Redeploy complete!$(NC)"
	@echo ""
	@echo "$(YELLOW)Service URLs (all via port 4200):$(NC)"
	@echo "  - Frontend: http://localhost:4200"
	@echo "  - Auth API Swagger: http://localhost:4200/swagger"
	@echo "  - Diagnostics Swagger: http://localhost:4200/diagnostics/swagger"
	@echo "  - RabbitMQ Management: http://localhost:4200/amqp/"

rebuild-webapi: rebuild-webapi-docker ## Rebuild the webapi Docker image (alias for rebuild-webapi-docker)

rebuild-audit-docker: ensure-services ## Build the audit service Docker image and create the container (uses cached base images, only rebuilding code changes)
	@echo "$(YELLOW)Rebuilding audit service Docker image...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) Docker is not running. Skipping image rebuild.$(NC)"; \
		echo "$(YELLOW)  Start Docker Desktop and run 'make rebuild-audit-image' to rebuild the image$(NC)"; \
		exit 1; \
	fi
	@if docker ps --filter "name=boilerplate-services-audit" --format "{{.Names}}" | grep -q "boilerplate-services-audit"; then \
		echo "$(YELLOW)  Stopping existing audit container...$(NC)"; \
		docker-compose stop audit 2>/dev/null || docker compose stop audit 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building audit service image (using cached base images, only rebuilding code changes)...$(NC)"
	@echo "$(YELLOW)  Using DOCKER_BUILDKIT=1 for parallel builds and better caching$(NC)"
	@echo "$(YELLOW)  If base images are not cached, this will fail. Cache them first with:$(NC)"
	@echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"
	@echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"
	@if command -v docker > /dev/null 2>&1; then \
		if DOCKER_BUILDKIT=1 docker build --pull=false -f BoilerPlate.Services.Audit/Dockerfile -t boilerplate-services-audit:latest . 2>&1; then \
			echo "$(GREEN)  $(OK) Audit service image built successfully (using cached base images)$(NC)"; \
			echo "$(YELLOW)  Creating audit container (not starting it)...$(NC)"; \
			if docker ps -a --filter "name=boilerplate-services-audit" --format "{{.Names}}" | grep -q "boilerplate-services-audit"; then \
				echo "$(GREEN)  $(OK) Audit container already exists$(NC)"; \
			else \
				echo "$(YELLOW)  Waiting for dependent services to be healthy (checking Docker health status)...$(NC)"; \
				for i in 1 2 3 4 5 6 7 8 9 10; do \
					RABBITMQ_HEALTH=$$(docker inspect --format='{{.State.Health.Status}}' rabbitmq-auth 2>/dev/null || echo "starting"); \
					MONGODB_HEALTH=$$(docker inspect --format='{{.State.Health.Status}}' mongodb-logs 2>/dev/null || echo "starting"); \
					if [ "$$RABBITMQ_HEALTH" = "healthy" ] && [ "$$MONGODB_HEALTH" = "healthy" ]; then \
						echo "$(GREEN)  $(OK) All dependent services are healthy$(NC)"; \
						break; \
					fi; \
					if [ $$i -lt 10 ]; then \
						echo "$(YELLOW)  Waiting... (RabbitMQ: $$RABBITMQ_HEALTH, MongoDB: $$MONGODB_HEALTH)$(NC)"; \
						sleep 3; \
					fi; \
				done; \
				if command -v docker-compose > /dev/null 2>&1; then \
					if docker-compose create --no-build audit 2>&1; then \
						echo "$(GREEN)  $(OK) Audit container created successfully$(NC)"; \
					else \
						if docker ps -a --filter "name=boilerplate-services-audit" --format="{{.Names}}" | grep -q "boilerplate-services-audit"; then \
							echo "$(GREEN)  $(OK) Audit container exists (was created)$(NC)"; \
						else \
							echo "$(YELLOW)  $(WARN) Container creation failed. This may be due to health check dependencies.$(NC)"; \
							echo "$(YELLOW)  The container will be created automatically when you run 'make docker-up-audit'$(NC)"; \
						fi; \
					fi; \
				elif command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then \
					if docker compose create --no-build audit 2>&1; then \
						echo "$(GREEN)  $(OK) Audit container created successfully$(NC)"; \
					else \
						if docker ps -a --filter "name=boilerplate-services-audit" --format="{{.Names}}" | grep -q "boilerplate-services-audit"; then \
							echo "$(GREEN)  $(OK) Audit container exists (was created)$(NC)"; \
						else \
							echo "$(YELLOW)  $(WARN) Container creation failed. This may be due to health check dependencies.$(NC)"; \
							echo "$(YELLOW)  The container will be created automatically when you run 'make docker-up-audit'$(NC)"; \
						fi; \
					fi; \
				fi; \
			fi; \
		else \
			BUILD_EXIT=$$?; \
			echo "$(RED)  $(FAIL) Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
			echo "$(YELLOW)  This likely means base images are not cached.$(NC)"; \
			echo "$(YELLOW)  To cache base images (run once):$(NC)"; \
			echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/aspnet:8.0$(NC)"; \
			echo "$(YELLOW)    docker pull mcr.microsoft.com/dotnet/sdk:8.0$(NC)"; \
			echo "$(YELLOW)  Then run 'make rebuild-audit-docker' again.$(NC)"; \
			exit 1; \
		fi; \
	else \
		echo "$(YELLOW)  $(WARN) Docker not available. Skipping rebuild.$(NC)"; \
		echo "$(YELLOW)  Rebuild manually later with 'make rebuild-audit-image'$(NC)"; \
		exit 1; \
	fi

rebuild-audit: rebuild-audit-docker ## Rebuild the audit service Docker image (alias for rebuild-audit-docker)

rebuild-event-logs-image: ## Build the event logs service Docker image
	@echo "$(YELLOW)Rebuilding event logs service Docker image...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(RED)  $(FAIL) Docker is not running. Please start Docker Desktop first.$(NC)"; \
		exit 1; \
	fi
	@if docker ps -a --filter "name=boilerplate-services-event-logs" --format "{{.Names}}" | grep -q "boilerplate-services-event-logs"; then \
		echo "$(YELLOW)  Stopping and removing existing event logs container...$(NC)"; \
		docker-compose stop event-logs 2>/dev/null || docker compose stop event-logs 2>/dev/null || true; \
		docker-compose rm -f event-logs 2>/dev/null || docker compose rm -f event-logs 2>/dev/null || docker rm -f boilerplate-services-event-logs 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building event logs service image...$(NC)"
	@if DOCKER_BUILDKIT=1 docker build --pull=false -f BoilerPlate.Services.EventLogs/Dockerfile -t boilerplate-services-event-logs:latest . 2>&1; then \
		echo "$(GREEN)  $(OK) Event logs service image built successfully$(NC)"; \
	else \
		echo "$(RED)  $(FAIL) Build failed$(NC)"; \
		exit 1; \
	fi

rebuild-event-logs-docker: ensure-services ## Build the event logs service Docker image and create the container
	@echo "$(YELLOW)Rebuilding event logs service Docker image...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) Docker is not running. Skipping image rebuild.$(NC)"; \
		exit 1; \
	fi
	@if docker ps --filter "name=boilerplate-services-event-logs" --format "{{.Names}}" | grep -q "boilerplate-services-event-logs"; then \
		docker-compose stop event-logs 2>/dev/null || docker compose stop event-logs 2>/dev/null || true; \
	fi
	@cd "$(or $(strip $(PROJECT_ROOT)),.)" && "$(MAKE)" rebuild-event-logs-image
	@docker-compose create --no-build event-logs 2>/dev/null || docker compose create --no-build event-logs 2>/dev/null || true

rebuild-event-logs: rebuild-event-logs-docker ## Rebuild the event logs service Docker image (alias)

docker-up-event-logs: ## Start only the event logs service
	@echo "$(YELLOW)Starting event logs service...$(NC)"
	@docker-compose up -d --no-deps event-logs 2>&1 || docker compose up -d --no-deps event-logs 2>&1 || true
	@echo "$(GREEN)$(OK) Event logs service started$(NC)"

docker-logs-event-logs: ## View logs from event logs service only
	@docker-compose logs -f event-logs || docker compose logs -f event-logs

rebuild-frontend-docker: ## Build the frontend Docker image
	@echo "$(YELLOW)Rebuilding frontend Docker image...$(NC)"
	@if ! docker info > /dev/null 2>&1; then \
		echo "$(YELLOW)  $(WARN) Docker is not running. Skipping image rebuild.$(NC)"; \
		echo "$(YELLOW)  Start Docker Desktop and run 'make rebuild-frontend-docker' to rebuild the image$(NC)"; \
		exit 1; \
	fi
	@if docker ps --filter "name=boilerplate-frontend" --format "{{.Names}}" | grep -q "boilerplate-frontend"; then \
		echo "$(YELLOW)  Stopping existing frontend container...$(NC)"; \
		docker-compose stop frontend 2>/dev/null || docker compose stop frontend 2>/dev/null || true; \
	fi
	@echo "$(YELLOW)  Building frontend image...$(NC)"
	@if command -v docker > /dev/null 2>&1; then \
		if DOCKER_BUILDKIT=1 docker build --pull=false -f BoilerPlate.Frontend/Dockerfile -t boilerplate-frontend:latest . 2>&1; then \
			echo "$(GREEN)  $(OK) Frontend image built successfully$(NC)"; \
		else \
			BUILD_EXIT=$$?; \
			echo "$(RED)  $(FAIL) Build failed (exit code: $$BUILD_EXIT)$(NC)"; \
			echo "$(YELLOW)  If you see 'error getting credentials', try:$(NC)"; \
			echo "$(YELLOW)    docker pull node:18-alpine && docker pull nginx:alpine$(NC)"; \
			echo "$(YELLOW)  Or restart Docker Desktop and run 'make rebuild-frontend-docker' again$(NC)"; \
			exit 1; \
		fi; \
	else \
		echo "$(YELLOW)  $(WARN) Docker not available. Skipping rebuild.$(NC)"; \
		exit 1; \
	fi

rebuild-frontend: rebuild-frontend-docker ## Rebuild the frontend Docker image (alias for rebuild-frontend-docker)

docker-build: rebuild-webapi-docker rebuild-audit-docker rebuild-event-logs-docker rebuild-diagnostics-docker rebuild-frontend-docker ## Build and start the webapi, audit, event-logs, diagnostics, and frontend services
	@echo "$(YELLOW)Starting services...$(NC)"
	@docker-compose up -d webapi audit event-logs diagnostics frontend || docker compose up -d webapi audit event-logs diagnostics frontend || true
	@echo "$(GREEN)$(OK) Services rebuilt and started$(NC)"

docker-logs: ## View logs from all services
	@docker-compose logs -f || docker compose logs -f

docker-logs-webapi: ## View logs from webapi service only
	@docker-compose logs -f webapi || docker compose logs -f webapi

docker-logs-audit: ## View logs from audit service only
	@docker-compose logs -f audit || docker compose logs -f audit

docker-logs-diagnostics: ## View logs from diagnostics API service only
	@docker-compose logs -f diagnostics || docker compose logs -f diagnostics

docker-logs-frontend: ## View logs from frontend service only
	@docker-compose logs -f frontend || docker compose logs -f frontend

test: ## Run all unit tests
	@cd "$(PROJECT_ROOT)" && echo "$(YELLOW)Running unit tests...$(NC)" && dotnet test BoilerPlate.sln --verbosity normal

verify: ## Verify the setup
	@echo "$(YELLOW)Verifying setup...$(NC)"
	@echo ""
	@echo "$(YELLOW)1. Checking JWT keys:$(NC)"
	@if [ -f jwt-keys/private_key.pem ] && [ -f jwt-keys/public_key.pem ]; then \
		echo "$(GREEN)  $(OK) JWT keys exist$(NC)"; \
		openssl rsa -in jwt-keys/private_key.pem -check -noout > /dev/null 2>&1 && echo "$(GREEN)  $(OK) Private key is valid$(NC)" || echo "$(RED)  $(FAIL) Private key is invalid$(NC)"; \
	else \
		echo "$(RED)  $(FAIL) JWT keys are missing$(NC)"; \
	fi
	@echo ""
	@echo "$(YELLOW)2. Checking .env file:$(NC)"
	@if [ -f .env ]; then \
		echo "$(GREEN)  $(OK) .env file exists$(NC)"; \
		grep -q "JWT_PRIVATE_KEY=" .env && echo "$(GREEN)  $(OK) JWT_PRIVATE_KEY is set$(NC)" || echo "$(RED)  $(FAIL) JWT_PRIVATE_KEY is missing$(NC)"; \
		grep -q "JWT_PUBLIC_KEY=" .env && echo "$(GREEN)  $(OK) JWT_PUBLIC_KEY is set$(NC)" || echo "$(RED)  $(FAIL) JWT_PUBLIC_KEY is missing$(NC)"; \
	else \
		echo "$(RED)  $(FAIL) .env file is missing$(NC)"; \
	fi
	@echo ""
	@echo "$(YELLOW)3. Checking Docker services:$(NC)"
	@docker ps --filter "name=postgres-auth" --format "{{.Names}}" | grep -q "postgres-auth" && echo "$(GREEN)  $(OK) PostgreSQL is running$(NC)" || echo "$(YELLOW)  $(NONE) PostgreSQL is not running$(NC)"
	@docker ps --filter "name=rabbitmq-auth" --format "{{.Names}}" | grep -q "rabbitmq-auth" && echo "$(GREEN)  $(OK) RabbitMQ is running$(NC)" || echo "$(YELLOW)  $(NONE) RabbitMQ is not running$(NC)"
	@docker ps --filter "name=mongodb-logs" --format "{{.Names}}" | grep -q "mongodb-logs" && echo "$(GREEN)  $(OK) MongoDB is running$(NC)" || echo "$(YELLOW)  $(NONE) MongoDB is not running$(NC)"
	@docker ps --filter "name=otel-collector" --format "{{.Names}}" | grep -q "otel-collector" && echo "$(GREEN)  $(OK) OTEL Collector is running$(NC)" || echo "$(YELLOW)  $(NONE) OTEL Collector is not running$(NC)"
	@docker ps --filter "name=boilerplate-auth-api" --format "{{.Names}}" | grep -q "boilerplate-auth-api" && echo "$(GREEN)  $(OK) WebAPI is running$(NC)" || echo "$(YELLOW)  $(NONE) WebAPI is not running$(NC)"
	@docker ps --filter "name=boilerplate-diagnostics-api" --format "{{.Names}}" | grep -q "boilerplate-diagnostics-api" && echo "$(GREEN)  $(OK) Diagnostics API is running$(NC)" || echo "$(YELLOW)  $(NONE) Diagnostics API is not running$(NC)"
	@docker ps --filter "name=boilerplate-frontend" --format "{{.Names}}" | grep -q "boilerplate-frontend" && echo "$(GREEN)  $(OK) Frontend is running$(NC)" || echo "$(YELLOW)  $(NONE) Frontend is not running$(NC)"

clean: ## Remove generated files (keeps JWT keys)
	@echo "$(YELLOW)Cleaning generated files...$(NC)"
	@rm -f .env
	@rm -f jwt-keys/*_base64.txt
	@rm -f jwt-keys/*_single_line.txt
	@echo "$(GREEN)$(OK) Cleaned generated files$(NC)"
	@echo "$(YELLOW)Note: JWT key files (.pem) were preserved$(NC)"

clean-all: ## Remove all generated files including JWT keys ($(WARN)️ WARNING)
	@echo "$(RED)$(WARN)️  WARNING: This will delete all JWT keys!$(NC)"
	@read -p "Are you sure? [y/N] " -n 1 -r; \
	echo; \
	if [[ $$REPLY =~ ^[Yy]$$ ]]; then \
		rm -rf jwt-keys/*; \
		rm -f .env; \
		echo "$(GREEN)$(OK) All files cleaned$(NC)"; \
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
	@echo "$(GREEN)  $(OK) New keys generated$(NC)"
	@echo "$(YELLOW)  Run 'make setup-env' to update .env file$(NC)"
