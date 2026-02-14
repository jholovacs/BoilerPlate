# PowerShell script to generate self-signed TLS certificates for development
# Called by Makefile on Windows or run manually: .\setup-tls-certs.ps1
# Requires OpenSSL in PATH (install via Git for Windows, Chocolatey, or use WSL: make setup-tls-certs)

$ErrorActionPreference = "Stop"
$tlsDir = "tls-certs"
$caPem = Join-Path $tlsDir "ca.pem"

if (Test-Path $caPem) {
    Write-Host "  TLS certificates already exist" -ForegroundColor Green
    exit 0
}

Write-Host "Setting up TLS certificates for development..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $tlsDir | Out-Null

# Generate CA
$caKey = Join-Path $tlsDir "ca-key.pem"
openssl genrsa -out $caKey 2048 2>$null
openssl req -x509 -new -nodes -key $caKey -sha256 -days 365 -out $caPem -subj "/CN=BoilerPlate Dev CA" 2>$null

# Generate server cert with SAN
$keyPem = Join-Path $tlsDir "key.pem"
$certCsr = Join-Path $tlsDir "cert.csr"
$certPem = Join-Path $tlsDir "cert.pem"
$opensslCnf = Join-Path $tlsDir "openssl.cnf"

openssl genrsa -out $keyPem 2048 2>$null
openssl req -new -key $keyPem -out $certCsr -subj "/CN=localhost" -config $opensslCnf 2>$null
openssl x509 -req -in $certCsr -CA $caPem -CAkey $caKey -CAcreateserial -out $certPem -days 365 -sha256 -extensions v3_req -extfile $opensslCnf 2>$null

# Cleanup
Remove-Item $caKey, $certCsr -ErrorAction SilentlyContinue
if (Test-Path (Join-Path $tlsDir "ca.srl")) { Remove-Item (Join-Path $tlsDir "ca.srl") -ErrorAction SilentlyContinue }

Write-Host "  TLS certificates generated (replace with trusted cert in production)" -ForegroundColor Green
