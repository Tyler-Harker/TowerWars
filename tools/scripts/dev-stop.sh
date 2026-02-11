#!/bin/bash

# TowerWars Development Stop Script

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "Stopping TowerWars development environment..."

cd "$ROOT_DIR/deploy/docker"
docker-compose -f docker-compose.dev.yaml down

echo "Development environment stopped."
