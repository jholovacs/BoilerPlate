# BoilerPlate

Multi-tenant authentication and diagnostics platform with OAuth2/OIDC, event logging, audit logs, and RabbitMQ management.

## Before You Start

**Verify prerequisites** for your chosen deployment:

```bash
./verify-prerequisites.sh docker            # Development
./verify-prerequisites.sh docker-production # Production (Docker)
./verify-prerequisites.sh k8s               # Kubernetes
```

Or with Make: `make verify-prereqs MODE=docker-production`

The script checks required tools and prints install commands for anything missing. See [PREREQUISITES.md](PREREQUISITES.md) for details.

---

## Deployment Options

| Option | Use Case | Command / Script |
|--------|----------|------------------|
| **Docker Compose (dev)** | Local development | `make setup` |
| **Docker Compose (production)** | Single-host production | `make setup-production` |
| **Kubernetes** | Cluster production | `./install-k8s.sh` |

---

## Quick Start

### Option 1: Docker Compose (Development)

Best for local development. All services run in Docker; frontend on HTTPS port 4200.

```bash
./verify-prerequisites.sh docker   # Check first
make setup
```

Then open **https://localhost:4200** (accept the self-signed certificate).

See [SETUP.md](SETUP.md) for details.

---

### Option 2: Docker Compose (Production)

Single-port deployment (HTTPS 443 only). No internal service ports exposed.

```bash
./verify-prerequisites.sh docker-production   # Check first
cp .env.production.example .env
# Edit .env with strong passwords and JWT_ISSUER_URL
make setup-production
```

**External services:** To use your own PostgreSQL, MongoDB, or RabbitMQ (managed instances), add `docker-compose.external-services.yml` and set `POSTGRES_CONNECTION_STRING`, `MONGODB_CONNECTION_STRING`, `RABBITMQ_CONNECTION_STRING` in `.env`.

TLS certs: place in `tls-certs/` or set `TLS_CERT_PATH`, `TLS_KEY_PATH` in `.env`

See [PRODUCTION_HARDENING.md](PRODUCTION_HARDENING.md) for full guide.

---

### Option 3: Kubernetes (Production)

Install into a Kubernetes cluster with guided prompts.

```bash
./verify-prerequisites.sh k8s   # Check first
./install-k8s.sh
```

The script will prompt for:

- **Namespace** – Target namespace (default: `boilerplate`)
- **Domain** – Public hostname (e.g. `app.example.com`)
- **Image registry** – Where to push/pull images (e.g. `ghcr.io/myorg`)
- **PostgreSQL** – Internal (deployed in cluster) or external (your managed instance)
- **MongoDB** – Internal or external (e.g. MongoDB Atlas)
- **RabbitMQ** – Internal or external (e.g. CloudAMQP, Amazon MQ)
- **Passwords** – For internal services only; external services use your connection strings
- **TLS** – cert-manager, existing secret, or self-signed
- **Build images** – Build and push Docker images now (y/n)

**Example session:**

```
Namespace to install into [boilerplate]: boilerplate
Public domain for the application: app.mycompany.com
Container image registry: ghcr.io/mycompany
PostgreSQL password: (generated)
...
Build and push Docker images now? (y/n) [y]: y
```

---

## Architecture

```
                    ┌─────────────────┐
                    │    Ingress /    │
                    │  Load Balancer  │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │    Frontend     │  (Angular + nginx)
                    │  (SPA + proxy)  │
                    └────────┬────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
┌───────▼───────┐   ┌────────▼────────┐   ┌───────▼───────┐
│  Auth WebAPI  │   │  Diagnostics    │   │   RabbitMQ    │
│  (OAuth2/JWT) │   │  API (OData)    │   │  (queues)     │
└───────┬───────┘   └────────┬────────┘   └───────────────┘
        │                    │
        │            ┌───────┴───────┐
        │            │               │
┌───────▼───────┐    │  ┌────────────▼────────────┐
│  PostgreSQL   │    │  │  MongoDB (logs/audit)   │
└───────────────┘    │  └─────────────────────────┘
                     │
                     │  ┌────────────────────────┐
                     └──│  Audit / Event-Logs    │
                        │  (background services) │
                        └────────────────────────┘
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [PREREQUISITES.md](PREREQUISITES.md) | Required tools, install commands |
| [SETUP.md](SETUP.md) | Quick setup, Makefile commands |
| [DOCKER_STARTUP_GUIDE.md](DOCKER_STARTUP_GUIDE.md) | Detailed Docker setup |
| [PRODUCTION_HARDENING.md](PRODUCTION_HARDENING.md) | Production hardening (certs, ports) |
| [k8s/](k8s/) | Kubernetes manifests |

---

## Default Credentials (Development)

| Service | Username | Password |
|---------|----------|----------|
| Application (admin) | admin | AdminPassword123! |
| PostgreSQL | boilerplate | SecurePassword123! |
| RabbitMQ | admin | SecurePassword123! |
| MongoDB | admin | SecurePassword123! |

**Change all passwords in production.**

---

## License

See repository for license information.
