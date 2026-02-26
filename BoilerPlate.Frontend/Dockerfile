# Build stage
FROM node:18-alpine AS build

WORKDIR /app

# Copy package files
COPY BoilerPlate.Frontend/package.json ./
COPY BoilerPlate.Frontend/angular.json ./
COPY BoilerPlate.Frontend/tsconfig.json ./
COPY BoilerPlate.Frontend/tsconfig.app.json ./

# Install dependencies
RUN npm install

# Copy source files
COPY BoilerPlate.Frontend/src ./src

# Copy build scripts (generate-build-info.js for versioning)
COPY BoilerPlate.Frontend/scripts ./scripts

# Build the application (increase Node memory for faster builds)
ENV NODE_OPTIONS="--max-old-space-size=4096"
RUN npm run build

# Production stage
FROM nginx:alpine

# Copy built files from build stage
COPY --from=build /app/dist/boilerplate-frontend /usr/share/nginx/html

# Override auth config for production (same-origin; nginx proxies /oauth, /api, /diagnostics, /amqp)
COPY BoilerPlate.Frontend/auth-api.config.production.json /usr/share/nginx/html/auth-api.config.json

# Copy nginx configuration
COPY BoilerPlate.Frontend/nginx.conf /etc/nginx/conf.d/default.conf

# Expose ports (80 redirects to 443; HTTPS for OAuth)
EXPOSE 80 443

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
