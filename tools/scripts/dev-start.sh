#!/bin/bash

# TowerWars Development Startup Script

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "Starting TowerWars development environment..."

# Start Docker infrastructure
echo "Starting Redis and PostgreSQL..."
cd "$ROOT_DIR/deploy/docker"
docker-compose -f docker-compose.dev.yaml up -d

# Wait for services to be ready
echo "Waiting for services to be ready..."
sleep 5

# Check if Redis is ready
until docker exec towerwars-redis redis-cli ping > /dev/null 2>&1; do
    echo "Waiting for Redis..."
    sleep 1
done
echo "Redis is ready!"

# Check if PostgreSQL is ready
until docker exec towerwars-postgres pg_isready -U dev > /dev/null 2>&1; do
    echo "Waiting for PostgreSQL..."
    sleep 1
done
echo "PostgreSQL is ready!"

echo ""
echo "Infrastructure is running!"
echo ""
echo "To start services, run in separate terminals:"
echo ""
echo "  Auth Service:"
echo "    cd $ROOT_DIR/src/TowerWars.Auth && dotnet run"
echo ""
echo "  Zone Server:"
echo "    cd $ROOT_DIR/src/TowerWars.ZoneServer && dotnet run"
echo ""
echo "  Gateway:"
echo "    cd $ROOT_DIR/src/TowerWars.Gateway && dotnet run"
echo ""
echo "To stop infrastructure:"
echo "  cd $ROOT_DIR/deploy/docker && docker-compose -f docker-compose.dev.yaml down"
