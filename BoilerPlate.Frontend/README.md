# BoilerPlate Frontend

Angular frontend application for BoilerPlate Authentication system.

## Features

- User authentication via OAuth2 password grant
- Tenant management for Service Administrators
  - Browse and search tenants
  - Edit tenant information
  - Delete tenants
- Event logs and audit logs (diagnostics) for Service and Tenant Administrators
- RabbitMQ Management with automatic login for Service Administrators

## Access

### URLs

| Environment | URL | Notes |
|-------------|-----|-------|
| Local dev (`ng serve`) | http://localhost:4200 | Auth API typically at http://localhost:8080 (see `auth-api.config.json`) |
| Docker (nginx) | https://localhost:4200 | Same origin; API proxied at `/api`, RabbitMQ at `/amqp` |

### Role-Based Access

| Feature | Roles |
|---------|-------|
| Account, Change password | All authenticated users |
| My tenant | Tenant Administrator, User Administrator, Role Administrator |
| Tenants | Service Administrator only |
| RabbitMQ Management | Service Administrator only (auto-login via proxy) |
| Event logs, Audit logs | Service Administrator, Tenant Administrator |

### RabbitMQ Management

Service Administrators see a **RabbitMQ Management** link in the side nav. Clicking it:

1. Requests a short-lived access token from `/api/rabbitmq/access`
2. Opens the RabbitMQ Management UI in a new tab with credentials automatically supplied
3. No manual login required (admin credentials from `RABBITMQ_CONNECTION_STRING` are injected by the proxy)

If the access request fails, the link falls back to `/amqp/` (nginx proxy) where the user can log in manually.

### Diagnostics (Event Logs, Audit Logs)

Service and Tenant Administrators can access:

- **Event logs** – Real-time application logs (Serilog → RabbitMQ → MongoDB)
- **Audit logs** – User and entity change history

These are served via the diagnostics API at `/diagnostics/` (proxied by nginx in Docker).

## Development

### Prerequisites

- Node.js 18+
- npm or yarn

### Running Locally

```bash
# Install dependencies
npm install

# Start development server
npm start

# The app will be available at http://localhost:4200
```

### Building for Production

```bash
npm run build
```

## Docker

### Build and Run

```bash
# Build the Docker image
docker build -t boilerplate-frontend:latest -f BoilerPlate.Frontend/Dockerfile .

# Or use docker-compose (from project root)
docker-compose up frontend
```

The frontend will be available at http://localhost:4200

## Configuration

### Authentication API base URL (`auth://`)

Auth-related requests use the `auth://` URL scheme in code (e.g. `auth://oauth/token`). The actual base URL is set in **`src/auth-api.config.json`**.

Edit `src/auth-api.config.json`:

```json
{
  "authApiBaseUrl": "http://localhost:8080"
}
```

- **Local dev (auth API on another port):** Set to your auth server, e.g. `http://localhost:8080`.
- **Same-origin (e.g. reverse proxy or Docker):** Set to `""` so `auth://` paths become same-origin (e.g. `/oauth/token`).
- **Production:** Set to your auth API origin, e.g. `https://auth.example.com`.

The file is loaded at app startup and is included in the build output so it can be replaced per environment without rebuilding.

The frontend also proxies `/api` (and when using same-origin auth, `/oauth`) to the backend when using the dev server; nginx handles this routing in Docker.

## API Endpoints

The frontend communicates with the following API endpoints:

### Authentication API (`auth://`)

- `POST auth://oauth/token` – Login (resolved via `auth-api.config.json`)

### Tenants

- `GET /api/tenants` – Get all tenants
- `GET /api/tenants/{id}` – Get tenant by ID
- `POST /api/tenants` – Create tenant
- `PUT /api/tenants/{id}` – Update tenant
- `DELETE /api/tenants/{id}` – Delete tenant

### RabbitMQ Proxy (Service Administrators)

- `GET auth://api/rabbitmq/access` – Obtain access cookie and proxy URL (Bearer token required)
- `GET /api/rabbitmq/` – Proxied RabbitMQ Management UI (cookie required)

### Diagnostics

- `/diagnostics/odata/EventLogs` – Event logs OData
- `/diagnostics/odata/AuditLogs` – Audit logs OData
