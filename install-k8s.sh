#!/usr/bin/env bash
# BoilerPlate Kubernetes Installer
# Prompts for production settings and installs the system into a Kubernetes cluster.
# Prerequisites: kubectl, docker (for building images), optionally kustomize

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR" && pwd)"
K8S_DIR="$PROJECT_ROOT/k8s"
OVERLAY_DIR="$K8S_DIR/overlays/production"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# --- Prompts ---

prompt() {
  local var="$1"
  local prompt="$2"
  local default="${3:-}"
  local secret="${4:-false}"

  if [ -n "${!var}" ]; then
    return 0
  fi

  if [ "$secret" = "true" ]; then
    read -r -s -p "$prompt ${default:+[$default] }: " val
    echo
  else
    read -r -p "$prompt ${default:+[$default] }: " val
  fi
  eval "$var=\"${val:-$default}\""
}

generate_password() {
  openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 24
}

# --- Checks ---

check_prereqs() {
  log_info "Checking prerequisites..."
  if [ -x "$SCRIPT_DIR/verify-prerequisites.sh" ] || [ -f "$SCRIPT_DIR/verify-prerequisites.sh" ]; then
    if ! "$SCRIPT_DIR/verify-prerequisites.sh" k8s; then
      log_error "Run: ./verify-prerequisites.sh k8s"
      log_error "Then install any missing tools and try again."
      exit 1
    fi
  else
    command -v kubectl >/dev/null 2>&1 || { log_error "kubectl is required. Run: ./verify-prerequisites.sh k8s"; exit 1; }
    kubectl cluster-info >/dev/null 2>&1 || { log_error "Cannot connect to cluster. Check kubectl config."; exit 1; }
  fi
  log_info "Prerequisites OK"
}

# --- Main ---

main() {
  echo ""
  echo "=========================================="
  echo "  BoilerPlate Kubernetes Installer"
  echo "=========================================="
  echo ""

  check_prereqs

  # Prompts
  prompt NAMESPACE "Namespace to install into" "boilerplate"
  prompt DOMAIN "Public domain for the application (e.g. app.example.com)" ""
  prompt IMAGE_REGISTRY "Container image registry (e.g. ghcr.io/myorg or docker.io/myuser)" ""

  if [ -z "$IMAGE_REGISTRY" ]; then
    log_warn "Image registry is required. Build and push images first, or specify a registry."
    prompt IMAGE_REGISTRY "Image registry" ""
  fi

  # Remove trailing slash from registry
  IMAGE_REGISTRY="${IMAGE_REGISTRY%/}"

  # --- Data services: internal (deployed in cluster) or external (managed instance) ---
  log_info "Data services: use internal (deployed in cluster) or external (your managed PostgreSQL, MongoDB, RabbitMQ)?"
  echo ""

  # PostgreSQL
  echo "  PostgreSQL (auth database):"
  echo "    1) Internal - deploy PostgreSQL in cluster (default)"
  echo "    2) External - use my own (Azure Database, AWS RDS, etc.)"
  prompt POSTGRES_MODE "Choice [1-2]" "1"
  USE_INTERNAL_POSTGRES="true"
  if [ "$POSTGRES_MODE" = "2" ]; then
    USE_INTERNAL_POSTGRES="false"
    echo "    Example: Host=myserver.postgres.database.azure.com;Port=5432;Database=BoilerPlateAuth;Username=myuser;Password=xxx;Ssl Mode=Require"
    prompt POSTGRES_CONNECTION "PostgreSQL connection string" "" true
    if [ -z "$POSTGRES_CONNECTION" ]; then
      log_error "PostgreSQL connection string is required for external mode."
      exit 1
    fi
  else
    prompt POSTGRES_PASSWORD "PostgreSQL password (internal)" "$(generate_password)" true
    POSTGRES_CONNECTION="Host=postgres;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=${POSTGRES_PASSWORD}"
  fi

  # MongoDB (audit logs and event logs can use different connections)
  echo ""
  echo "  MongoDB (audit logs and event logs - can use same or different connections):"
  echo "    1) Internal - deploy MongoDB in cluster (default)"
  echo "    2) External - use my own (MongoDB Atlas, Azure Cosmos DB, etc.)"
  prompt MONGODB_MODE "Choice [1-2]" "1"
  USE_INTERNAL_MONGODB="true"
  if [ "$MONGODB_MODE" = "2" ]; then
    USE_INTERNAL_MONGODB="false"
    echo "    Example: mongodb+srv://user:pass@cluster.mongodb.net/logs?retryWrites=true&w=majority"
    prompt AUDIT_LOGS_MONGODB_CONNECTION "Audit logs MongoDB connection string" "" true
    if [ -z "$AUDIT_LOGS_MONGODB_CONNECTION" ]; then
      log_error "Audit logs MongoDB connection string is required for external mode."
      exit 1
    fi
    prompt EVENT_LOGS_MONGODB_CONNECTION "Event logs MongoDB connection string (leave empty to use same as audit logs)" "" true
    if [ -z "$EVENT_LOGS_MONGODB_CONNECTION" ]; then
      EVENT_LOGS_MONGODB_CONNECTION="$AUDIT_LOGS_MONGODB_CONNECTION"
    fi
  else
    prompt MONGODB_PASSWORD "MongoDB password (internal)" "$(generate_password)" true
    AUDIT_LOGS_MONGODB_CONNECTION="mongodb://admin:${MONGODB_PASSWORD}@mongodb:27017/audit?authSource=admin"
    EVENT_LOGS_MONGODB_CONNECTION="mongodb://admin:${MONGODB_PASSWORD}@mongodb:27017/logs?authSource=admin"
  fi

  # RabbitMQ
  echo ""
  echo "  RabbitMQ (message bus):"
  echo "    1) Internal - deploy RabbitMQ in cluster (default)"
  echo "    2) External - use my own (CloudAMQP, Amazon MQ, etc.)"
  prompt RABBITMQ_MODE "Choice [1-2]" "1"
  USE_INTERNAL_RABBITMQ="true"
  if [ "$RABBITMQ_MODE" = "2" ]; then
    USE_INTERNAL_RABBITMQ="false"
    echo "    Example: amqps://user:pass@host.cloudamqp.com/vhost"
    prompt RABBITMQ_CONNECTION "RabbitMQ connection string" "" true
    if [ -z "$RABBITMQ_CONNECTION" ]; then
      log_error "RabbitMQ connection string is required for external mode."
      exit 1
    fi
  else
    prompt RABBITMQ_PASSWORD "RabbitMQ password (internal)" "$(generate_password)" true
    RABBITMQ_CONNECTION="amqp://admin:${RABBITMQ_PASSWORD}@rabbitmq:5672/"
  fi

  prompt ADMIN_PASSWORD "Admin user password (first login)" "$(generate_password)" true
  prompt ADMIN_USERNAME "Admin username" "admin"

  # JWT keys
  if [ ! -f "$PROJECT_ROOT/jwt-keys/private_key.pem" ] || [ ! -f "$PROJECT_ROOT/jwt-keys/public_key.pem" ]; then
    log_info "Generating JWT keys..."
    mkdir -p "$PROJECT_ROOT/jwt-keys"
    openssl genrsa -out "$PROJECT_ROOT/jwt-keys/private_key.pem" 2048 2>/dev/null
    openssl rsa -in "$PROJECT_ROOT/jwt-keys/private_key.pem" -pubout -out "$PROJECT_ROOT/jwt-keys/public_key.pem" 2>/dev/null
  fi

  JWT_PRIVATE_KEY=$(cat "$PROJECT_ROOT/jwt-keys/private_key.pem" | base64 | tr -d '\n')
  JWT_PUBLIC_KEY=$(cat "$PROJECT_ROOT/jwt-keys/public_key.pem" | base64 | tr -d '\n')

  JWT_ISSUER_URL="https://${DOMAIN}"

  # TLS
  log_info "TLS certificate for Ingress..."
  echo "  1) Use cert-manager (requires cert-manager installed)"
  echo "  2) Provide existing TLS secret name"
  echo "  3) Create self-signed cert (development only)"
  prompt TLS_OPTION "Choose option [1-3]" "1"

  TLS_SECRET_NAME="boilerplate-tls"
  if [ "$TLS_OPTION" = "2" ]; then
    prompt TLS_SECRET_NAME "Existing TLS secret name" "boilerplate-tls"
  elif [ "$TLS_OPTION" = "3" ]; then
    log_info "Creating self-signed certificate..."
    mkdir -p "$PROJECT_ROOT/tls-certs"
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
      -keyout "$PROJECT_ROOT/tls-certs/key.pem" \
      -out "$PROJECT_ROOT/tls-certs/cert.pem" \
      -subj "/CN=${DOMAIN}" \
      -addext "subjectAltName=DNS:${DOMAIN}" 2>/dev/null || \
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
      -keyout "$PROJECT_ROOT/tls-certs/key.pem" \
      -out "$PROJECT_ROOT/tls-certs/cert.pem" \
      -subj "/CN=${DOMAIN}" 2>/dev/null
  fi

  # Create overlay
  log_info "Creating Kustomize overlay..."
  mkdir -p "$OVERLAY_DIR"

  # Patch files for domain and namespace
  cat > "$OVERLAY_DIR/ingress-patch.yaml" << PATCH
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: boilerplate
  namespace: ${NAMESPACE}
  annotations:
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/proxy-body-size: "10m"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - ${DOMAIN}
      secretName: ${TLS_SECRET_NAME}
  rules:
    - host: ${DOMAIN}
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: frontend
                port:
                  number: 80
PATCH

  cat > "$OVERLAY_DIR/namespace-patch.yaml" << PATCH
apiVersion: v1
kind: Namespace
metadata:
  name: ${NAMESPACE}
  labels:
    app.kubernetes.io/name: boilerplate
PATCH

  # Build components list based on internal/external choices
  COMPONENTS=""
  [ "$USE_INTERNAL_POSTGRES" = "true" ] && COMPONENTS="${COMPONENTS}  - ../../components/infrastructure-postgres
"
  [ "$USE_INTERNAL_MONGODB" = "true" ] && COMPONENTS="${COMPONENTS}  - ../../components/infrastructure-mongodb
"
  [ "$USE_INTERNAL_RABBITMQ" = "true" ] && COMPONENTS="${COMPONENTS}  - ../../components/infrastructure-rabbitmq
"

  COMPONENTS_SECTION=""
  if [ -n "$COMPONENTS" ]; then
    COMPONENTS_SECTION="components:
${COMPONENTS}
"
  fi

  cat > "$OVERLAY_DIR/kustomization.yaml" << EOF
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: ${NAMESPACE}

resources:
  - ../../base

${COMPONENTS_SECTION}
patches:
  - path: namespace-patch.yaml
    target:
      kind: Namespace
      name: boilerplate
  - path: ingress-patch.yaml
    target:
      kind: Ingress
      name: boilerplate

images:
  - name: boilerplate-authentication-webapi:latest
    newName: ${IMAGE_REGISTRY}/boilerplate-authentication-webapi
    newTag: latest
  - name: boilerplate-services-audit:latest
    newName: ${IMAGE_REGISTRY}/boilerplate-services-audit
    newTag: latest
  - name: boilerplate-services-event-logs:latest
    newName: ${IMAGE_REGISTRY}/boilerplate-services-event-logs
    newTag: latest
  - name: boilerplate-diagnostics-webapi:latest
    newName: ${IMAGE_REGISTRY}/boilerplate-diagnostics-webapi
    newTag: latest
  - name: boilerplate-frontend:latest
    newName: ${IMAGE_REGISTRY}/boilerplate-frontend
    newTag: latest
EOF

  # Replace namespace in base (kustomize will handle it, but base has hardcoded boilerplate)
  # We need to patch the namespace in base resources - kustomize overlay sets namespace for all
  # The base kustomization has namespace: boilerplate. The overlay will override with NAMESPACE.
  # Good.

  # Create secrets
  log_info "Creating Kubernetes secrets..."
  kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

  SECRET_ARGS=(
    --from-literal=admin-username="$ADMIN_USERNAME"
    --from-literal=admin-password="$ADMIN_PASSWORD"
    --from-literal=postgres-connection="$POSTGRES_CONNECTION"
    --from-literal=audit-logs-mongodb-connection="$AUDIT_LOGS_MONGODB_CONNECTION"
    --from-literal=event-logs-mongodb-connection="$EVENT_LOGS_MONGODB_CONNECTION"
    --from-literal=rabbitmq-connection="$RABBITMQ_CONNECTION"
    --from-literal=jwt-private-key="$JWT_PRIVATE_KEY"
    --from-literal=jwt-public-key="$JWT_PUBLIC_KEY"
    --from-literal=jwt-issuer-url="$JWT_ISSUER_URL"
  )
  [ "$USE_INTERNAL_POSTGRES" = "true" ] && SECRET_ARGS+=(--from-literal=postgres-password="$POSTGRES_PASSWORD")
  [ "$USE_INTERNAL_MONGODB" = "true" ] && SECRET_ARGS+=(--from-literal=mongodb-password="$MONGODB_PASSWORD")
  [ "$USE_INTERNAL_RABBITMQ" = "true" ] && SECRET_ARGS+=(--from-literal=rabbitmq-password="$RABBITMQ_PASSWORD")

  kubectl create secret generic boilerplate-secrets -n "$NAMESPACE" \
    "${SECRET_ARGS[@]}" \
    --dry-run=client -o yaml | kubectl apply -f -

  # TLS secret
  if [ "$TLS_OPTION" = "3" ]; then
    kubectl create secret tls "$TLS_SECRET_NAME" -n "$NAMESPACE" \
      --cert="$PROJECT_ROOT/tls-certs/cert.pem" \
      --key="$PROJECT_ROOT/tls-certs/key.pem" \
      --dry-run=client -o yaml | kubectl apply -f -
  elif [ "$TLS_OPTION" = "1" ]; then
    log_info "Add cert-manager.io/cluster-issuer annotation to Ingress for automatic TLS."
    log_info "Or create secret manually: kubectl create secret tls $TLS_SECRET_NAME -n $NAMESPACE --cert=... --key=..."
  fi

  # Build and push images?
  prompt BUILD_IMAGES "Build and push Docker images now? (y/n)" "y"
  if [ "$BUILD_IMAGES" = "y" ] || [ "$BUILD_IMAGES" = "Y" ]; then
    log_info "Building images..."
    cd "$PROJECT_ROOT"
    docker build -t "${IMAGE_REGISTRY}/boilerplate-authentication-webapi:latest" -f BoilerPlate.Authentication.WebApi/Dockerfile .
    docker build -t "${IMAGE_REGISTRY}/boilerplate-services-audit:latest" -f BoilerPlate.Services.Audit/Dockerfile .
    docker build -t "${IMAGE_REGISTRY}/boilerplate-services-event-logs:latest" -f BoilerPlate.Services.EventLogs/Dockerfile .
    docker build -t "${IMAGE_REGISTRY}/boilerplate-diagnostics-webapi:latest" -f BoilerPlate.Diagnostics.WebApi/Dockerfile .
    docker build -t "${IMAGE_REGISTRY}/boilerplate-frontend:latest" -f BoilerPlate.Frontend/Dockerfile .
    log_info "Pushing images..."
    docker push "${IMAGE_REGISTRY}/boilerplate-authentication-webapi:latest"
    docker push "${IMAGE_REGISTRY}/boilerplate-services-audit:latest"
    docker push "${IMAGE_REGISTRY}/boilerplate-services-event-logs:latest"
    docker push "${IMAGE_REGISTRY}/boilerplate-diagnostics-webapi:latest"
    docker push "${IMAGE_REGISTRY}/boilerplate-frontend:latest"
  else
    log_warn "Ensure images are built and pushed to $IMAGE_REGISTRY before applying."
  fi

  # Apply
  log_info "Applying Kubernetes manifests..."
  kubectl apply -k "$OVERLAY_DIR"

  # Migrations (PostgreSQL - required for auth)
  if command -v dotnet >/dev/null 2>&1 && PATH="$HOME/.dotnet/tools:$PATH" dotnet ef --version >/dev/null 2>&1; then
    if [ "$USE_INTERNAL_POSTGRES" = "true" ]; then
      log_info "Waiting for PostgreSQL to be ready..."
      kubectl wait --for=condition=available deployment/postgres -n "$NAMESPACE" --timeout=120s 2>/dev/null || true
      log_info "Running database migrations..."
      cd "$PROJECT_ROOT"
      kubectl port-forward -n "$NAMESPACE" svc/postgres 5432:5432 &
      PF_PID=$!
      sleep 3
      (PATH="$HOME/.dotnet/tools:$PATH" ConnectionStrings__PostgreSqlConnection="Host=localhost;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=${POSTGRES_PASSWORD}" dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext 2>/dev/null) || log_warn "Migration failed or skipped. Run manually: kubectl port-forward svc/postgres 5432:5432, then make migrate"
      kill $PF_PID 2>/dev/null || true
    else
      log_info "Running database migrations against external PostgreSQL..."
      cd "$PROJECT_ROOT"
      (PATH="$HOME/.dotnet/tools:$PATH" ConnectionStrings__PostgreSqlConnection="$POSTGRES_CONNECTION" dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext 2>/dev/null) || log_warn "Migration failed. Run manually: ConnectionStrings__PostgreSqlConnection='<your-connection-string>' dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext"
    fi
  else
    log_warn "dotnet ef not found. Run migrations manually."
    if [ "$USE_INTERNAL_POSTGRES" = "true" ]; then
      log_warn "  kubectl port-forward -n $NAMESPACE svc/postgres 5432:5432"
      log_warn "  make migrate POSTGRES_PASSWORD=<password>"
    else
      log_warn "  ConnectionStrings__PostgreSqlConnection='<connection-string>' dotnet ef database update --project BoilerPlate.Authentication.Database.PostgreSql --startup-project BoilerPlate.Authentication.WebApi --context AuthenticationDbContext"
    fi
  fi

  echo ""
  log_info "Installation complete!"
  echo ""
  echo "  Application URL: https://${DOMAIN}"
  echo "  Namespace:       ${NAMESPACE}"
  echo "  Admin login:     ${ADMIN_USERNAME} / (password you set)"
  echo ""
  echo "  Check status:    kubectl get pods -n ${NAMESPACE}"
  echo "  View logs:       kubectl logs -f deployment/webapi -n ${NAMESPACE}"
  echo ""
}

main "$@"
