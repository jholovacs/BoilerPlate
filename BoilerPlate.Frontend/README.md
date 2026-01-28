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

The frontend is configured to proxy API requests to the backend at `/api`. The nginx configuration handles this routing.

## API Endpoints

The frontend communicates with the following API endpoints:

- `POST /api/oauth/token` - Authentication
- `GET /api/tenants` - Get all tenants
- `GET /api/tenants/{id}` - Get tenant by ID
- `POST /api/tenants` - Create tenant
- `PUT /api/tenants/{id}` - Update tenant
- `DELETE /api/tenants/{id}` - Delete tenant
