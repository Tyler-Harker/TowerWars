#!/bin/bash
echo "Cleaning up k3s TowerWars deployment..."
sudo k3s kubectl delete namespace towerwars --ignore-not-found=true
echo "k3s cleanup complete!"
echo ""
echo "For local development, use Docker Compose instead:"
echo "  ./dev-start.sh"
