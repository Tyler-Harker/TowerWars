#!/bin/bash

# TowerWars Development Environment Startup Script

set -e

echo "======================================"
echo "TowerWars Development Environment"
echo "======================================"
echo ""

# Check if docker-compose is available
if ! command -v docker &> /dev/null; then
    echo "Error: Docker is not installed or not in PATH"
    exit 1
fi

# Stop any existing containers
echo "Stopping any existing containers..."
docker-compose down 2>/dev/null || true

echo ""
echo "Building and starting services..."
echo "This may take a few minutes on first run..."
echo ""

# Build and start services
docker-compose up --build -d

echo ""
echo "Waiting for services to be healthy..."
sleep 10

# Show service status
echo ""
echo "Service Status:"
echo "======================================"
docker-compose ps

echo ""
echo "======================================"
echo "Development environment is ready!"
echo "======================================"
echo ""
echo "Services running at:"
echo "  - Gateway:       http://localhost:7000"
echo "  - Auth:          http://localhost:7001"
echo "  - World Manager: http://localhost:7002"
echo "  - Zone Server:   UDP port 7100"
echo "  - Social:        http://localhost:7004"
echo "  - PostgreSQL:    localhost:5432"
echo "  - Redis:         localhost:6379"
echo ""
echo "Debugging ports:"
echo "  - Auth:          5001"
echo "  - Gateway:       5000"
echo "  - World Manager: 5002"
echo "  - Zone Server:   5003"
echo "  - Social:        5004"
echo "  - Persistence:   5005"
echo ""
echo "To view logs:"
echo "  docker-compose logs -f [service-name]"
echo "  docker-compose logs -f auth"
echo "  docker-compose logs -f gateway"
echo ""
echo "To debug in VSCode:"
echo "  1. Press F5 or go to Run and Debug"
echo "  2. Select 'Docker: Attach to [Service]'"
echo "  3. Set breakpoints and debug!"
echo ""
echo "To stop all services:"
echo "  docker-compose down"
echo ""
echo "Hot reload is enabled - edit code and it will auto-reload!"
echo ""
