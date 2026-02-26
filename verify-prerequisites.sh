#!/usr/bin/env bash
# Verify prerequisites for BoilerPlate deployment.
# Usage: ./verify-prerequisites.sh [docker|docker-production|k8s]
# Run before install. Exit 0 = all OK; exit 1 = missing items (prints remediation).

set -e

MODE="${1:-docker}"
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

ok()  { echo -e "${GREEN}  [OK]${NC} $1"; }
fail() { echo -e "${RED}  [MISSING]${NC} $1"; echo -e "    ${YELLOW}$2${NC}"; FAILED=1; }
warn() { echo -e "${YELLOW}  [WARN]${NC} $1"; }

FAILED=0

check_cmd() {
  if command -v "$1" >/dev/null 2>&1; then
    ok "$1 ($($1 --version 2>/dev/null | head -1 || $1 version 2>/dev/null | head -1 || echo "installed"))"
    return 0
  else
    fail "$1" "$2"
    return 1
  fi
}

echo ""
echo "BoilerPlate Prerequisites Check ($MODE)"
echo "======================================"
echo ""

case "$MODE" in
  docker)
    check_cmd docker "Install: https://docs.docker.com/get-docker/"
    if command -v docker-compose >/dev/null 2>&1 || (command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1); then
      ok "docker-compose or docker compose"
    else
      fail "docker-compose" "Install Docker Desktop (includes Compose)"
    fi
    check_cmd openssl "macOS: built-in. Linux: apt install openssl. Windows: choco install openssl"
    check_cmd dotnet "Install: https://dotnet.microsoft.com/download"
    if command -v dotnet >/dev/null 2>&1; then
      dotnet ef --version >/dev/null 2>&1 || \
        (PATH="$HOME/.dotnet/tools:$PATH" dotnet ef --version >/dev/null 2>&1 && ok "dotnet-ef (installed)" || \
          warn "dotnet-ef: run 'dotnet tool install --global dotnet-ef' (Makefile will try to install)")
    fi
    check_cmd make "macOS: xcode-select --install. Linux: apt install build-essential. Windows: use setup.ps1"
    echo ""
    echo "Optional: Use setup.ps1 on Windows instead of Make."
    ;;

  docker-production)
    check_cmd docker "Install: https://docs.docker.com/get-docker/"
    if command -v docker-compose >/dev/null 2>&1 || (command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1); then
      ok "docker-compose or docker compose"
    else
      fail "docker-compose" "Install Docker Desktop"
    fi
    check_cmd openssl "macOS: built-in. Linux: apt install openssl"
    check_cmd dotnet "Install: https://dotnet.microsoft.com/download"
    if command -v dotnet >/dev/null 2>&1; then
      dotnet ef --version >/dev/null 2>&1 || PATH="$HOME/.dotnet/tools:$PATH" dotnet ef --version >/dev/null 2>&1 || \
        warn "dotnet-ef: run 'dotnet tool install --global dotnet-ef'"
    fi
    check_cmd make "Use 'make setup-production' or run steps manually"
    echo ""
    echo "TLS: Place cert.pem and key.pem in tls-certs/ or set TLS_CERT_PATH, TLS_KEY_PATH in .env"
    echo "     See PRODUCTION_HARDENING.md for Let's Encrypt or CA options."
    ;;

  k8s)
    check_cmd kubectl "Install: https://kubernetes.io/docs/tasks/tools/"
    if kubectl cluster-info >/dev/null 2>&1; then
      ok "kubectl can reach cluster"
    else
      fail "cluster access" "Run: kubectl config use-context <your-context>"
    fi
    if kubectl get ingressclass 2>/dev/null | grep -q nginx; then
      ok "nginx Ingress Controller"
    else
      warn "nginx Ingress: Install with 'kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.2/deploy/static/provider/cloud/deploy.yaml'"
    fi
    check_cmd openssl "For JWT keys and self-signed TLS"
    echo ""
    echo "For building images: docker must be installed."
    if command -v docker >/dev/null 2>&1; then
      ok "docker (for building images)"
    else
      warn "docker: Install if you want to build images during install. Otherwise push pre-built images."
    fi
    echo ""
    echo "TLS options: cert-manager (auto), existing secret, or self-signed (script can create)."
    ;;

  *)
    echo "Usage: $0 [docker|docker-production|k8s]"
    echo ""
    echo "  docker           - Development (make setup)"
    echo "  docker-production - Production single-host (make setup-production)"
    echo "  k8s              - Kubernetes (./install-k8s.sh)"
    exit 1
    ;;
esac

echo ""
if [ "$FAILED" -eq 1 ]; then
  echo -e "${RED}Some prerequisites are missing. Install them and run this script again.${NC}"
  exit 1
else
  echo -e "${GREEN}All required prerequisites are satisfied.${NC}"
  echo ""
  case "$MODE" in
    docker)           echo "Next: make setup";;
    docker-production) echo "Next: cp .env.production.example .env && edit .env && make setup-production";;
    k8s)             echo "Next: ./install-k8s.sh";;
  esac
  echo ""
  exit 0
fi

