#!/bin/bash

# TowerWars k3s Deployment Script

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "======================================"
echo "TowerWars k3s Deployment"
echo "======================================"

# Check if k3s is installed
if ! command -v k3s &> /dev/null; then
    echo "Error: k3s is not installed"
    echo "Install with: curl -sfL https://get.k3s.io | sh -"
    exit 1
fi

echo ""
echo "Step 1: Building Docker images..."
echo "======================================"

cd "$ROOT_DIR"

# Build all service images
services=("auth" "gateway" "zone-server" "world-manager" "social" "persistence")

for service in "${services[@]}"; do
    service_dir=$(echo $service | sed 's/-//g' | awk '{for(i=1;i<=NF;i++) $i=toupper(substr($i,1,1)) tolower(substr($i,2))}1' | sed 's/ //g')

    # Map service names to actual directory names
    case $service in
        "auth") service_dir="TowerWars.Auth" ;;
        "gateway") service_dir="TowerWars.Gateway" ;;
        "zone-server") service_dir="TowerWars.ZoneServer" ;;
        "world-manager") service_dir="TowerWars.WorldManager" ;;
        "social") service_dir="TowerWars.Social" ;;
        "persistence") service_dir="TowerWars.Persistence" ;;
    esac

    echo "Building towerwars/$service:latest..."
    docker build -t towerwars/$service:latest -f src/$service_dir/Dockerfile .

    echo "Importing into k3s..."
    sudo k3s ctr images import <(docker save towerwars/$service:latest) || \
    docker save towerwars/$service:latest | sudo k3s ctr images import -
done

echo ""
echo "Step 2: Creating k3s resources..."
echo "======================================"

# Create namespace
echo "Creating namespace..."
sudo k3s kubectl apply -f deploy/k3s/namespace.yaml

# Create ConfigMap and Secrets
echo "Creating ConfigMap and Secrets..."
sudo k3s kubectl create configmap towerwars-config \
    --from-literal=JWT_SECRET=your-super-secret-jwt-key-change-this-in-production \
    --from-literal=JWT_ISSUER=TowerWars.Auth \
    --from-literal=JWT_AUDIENCE=TowerWars \
    -n towerwars --dry-run=client -o yaml | sudo k3s kubectl apply -f -

sudo k3s kubectl create secret generic towerwars-secrets \
    --from-literal=POSTGRES_USER=dev \
    --from-literal=POSTGRES_PASSWORD=dev123 \
    -n towerwars --dry-run=client -o yaml | sudo k3s kubectl apply -f -

echo ""
echo "Step 3: Deploying infrastructure..."
echo "======================================"

# Deploy infrastructure (Redis, PostgreSQL)
echo "Deploying Redis..."
sudo k3s kubectl apply -f deploy/k3s/redis/

echo "Deploying PostgreSQL..."
sudo k3s kubectl apply -f deploy/k3s/postgres/

# Wait for infrastructure
echo "Waiting for infrastructure to be ready..."
sleep 10

echo ""
echo "Step 4: Deploying services..."
echo "======================================"

# Deploy services
echo "Deploying Auth service..."
sudo k3s kubectl apply -f deploy/k3s/auth/

echo "Deploying Gateway service..."
sudo k3s kubectl apply -f deploy/k3s/gateway/

echo "Deploying World Manager..."
sudo k3s kubectl apply -f deploy/k3s/world-manager/

echo "Deploying Zone Server..."
sudo k3s kubectl apply -f deploy/k3s/zone-server/

echo "Deploying Social service..."
sudo k3s kubectl apply -f deploy/k3s/social/

echo "Deploying Persistence service..."
sudo k3s kubectl apply -f deploy/k3s/persistence/

echo ""
echo "======================================"
echo "Deployment complete!"
echo "======================================"
echo ""
echo "Check status with:"
echo "  sudo k3s kubectl get pods -n towerwars"
echo "  sudo k3s kubectl get services -n towerwars"
echo ""
echo "View logs:"
echo "  sudo k3s kubectl logs -f deployment/auth -n towerwars"
echo "  sudo k3s kubectl logs -f deployment/gateway -n towerwars"
echo ""
echo "Port forward Gateway to access locally:"
echo "  sudo k3s kubectl port-forward -n towerwars service/gateway 7000:7000"
echo ""
