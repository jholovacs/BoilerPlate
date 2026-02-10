# BoilerPlate Frontend

Angular frontend application for BoilerPlate Authentication system.

## Features

- User authentication via OAuth2 password grant
- Tenant management for Service Administrators
  - Browse and search tenants
  - Edit tenant information
  - Delete tenants

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

- `POST auth://oauth/token` (resolved via `auth-api.config.json`) - Authentication
- `GET /api/tenants` - Get all tenants
- `GET /api/tenants/{id}` - Get tenant by ID
- `POST /api/tenants` - Create tenant
- `PUT /api/tenants/{id}` - Update tenant
- `DELETE /api/tenants/{id}` - Delete tenant
