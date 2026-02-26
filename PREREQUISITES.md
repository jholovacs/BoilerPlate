# Prerequisites

Run **`./verify-prerequisites.sh [mode]`** before installing. It checks required tools and prints install commands for anything missing.

| Mode | Command | For |
|------|---------|-----|
| `docker` | `./verify-prerequisites.sh docker` | Development (`make setup`) |
| `docker-production` | `./verify-prerequisites.sh docker-production` | Production single-host (`make setup-production`) |
| `k8s` | `./verify-prerequisites.sh k8s` | Kubernetes (`./install-k8s.sh`) |

---

## Quick Install Commands

### macOS (Homebrew)

```bash
# Development
brew install docker docker-compose openssl dotnet make

# Kubernetes only
brew install kubectl
```

### Linux (Ubuntu/Debian)

```bash
# Development
sudo apt update
sudo apt install -y docker.io docker-compose openssl dotnet-sdk-8.0 make

# Kubernetes only
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl
```

### Windows

- **Docker Desktop**: https://docs.docker.com/desktop/install/windows-install/
- **OpenSSL**: `choco install openssl` or use Git Bash (includes OpenSSL)
- **.NET SDK**: https://dotnet.microsoft.com/download
- **Kubectl**: `choco install kubernetes-cli`

Use `setup.ps1` instead of Make for Docker deployments.

---

## By Deployment Type

### Docker Compose (Development)

| Tool | Required | Install |
|------|----------|---------|
| Docker | Yes | [Get Docker](https://docs.docker.com/get-docker/) |
| Docker Compose | Yes | Included with Docker Desktop |
| OpenSSL | Yes | macOS: built-in. Linux: `apt install openssl` |
| .NET SDK 8.0+ | Yes | [Download](https://dotnet.microsoft.com/download) |
| dotnet-ef | Yes | `dotnet tool install --global dotnet-ef` |
| Make | Yes* | macOS: xcode-select. Linux: `apt install build-essential` |

*Windows: use `setup.ps1` instead of Make.

### Docker Compose (Production)

Same as development, plus:

| Item | Notes |
|------|-------|
| TLS certs | Place `cert.pem` and `key.pem` in `tls-certs/` or set `TLS_CERT_PATH`, `TLS_KEY_PATH` in `.env` |
| Strong passwords | Set in `.env` (see `.env.production.example`) |

### Kubernetes

| Tool | Required | Install |
|------|----------|---------|
| kubectl | Yes | [Install kubectl](https://kubernetes.io/docs/tasks/tools/) |
| Cluster access | Yes | `kubectl cluster-info` must succeed |
| nginx Ingress | Yes | [Install Ingress NGINX](https://kubernetes.github.io/ingress-nginx/deploy/) |
| OpenSSL | Yes | For JWT keys and self-signed TLS |
| Docker | For build | Only if building images during install |

Optional: **cert-manager** for automatic TLS (Let's Encrypt).

---

## One-Liner Verify Before Install

```bash
# Check before Docker production install
./verify-prerequisites.sh docker-production && make setup-production

# Check before Kubernetes install
./verify-prerequisites.sh k8s && ./install-k8s.sh
```
