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

# Build the application
RUN npm run build

# Production stage
FROM nginx:alpine

# Copy built files from build stage
COPY --from=build /app/dist/boilerplate-frontend /usr/share/nginx/html

# Copy nginx configuration
COPY BoilerPlate.Frontend/nginx.conf /etc/nginx/conf.d/default.conf

# Expose port
EXPOSE 80

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
