# TowerWars

A real-time multiplayer tower defense game built with .NET C#, Godot Engine, and Kubernetes.

## Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Gateway   │────▶│    Auth     │────▶│  PostgreSQL │
│  (Port 7000)│     │ (Port 7001) │     │ (Port 5432) │
└──────┬──────┘     └─────────────┘     └─────────────┘
       │
       │            ┌─────────────┐     ┌─────────────┐
       └───────────▶│World Manager│────▶│    Redis    │
                    │ (Port 7002) │     │ (Port 6379) │
                    └──────┬──────┘     └─────────────┘
                           │
                    ┌──────▼──────┐
                    │ Zone Server │
                    │ (Port 7100) │
                    │   (ENet)    │
                    └─────────────┘
```

## Game Modes

- **Solo**: Endless waves with increasing difficulty
- **Co-op (2-6 players)**: Team up against waves
- **PvP (2-16 players)**: Competitive matches

## Quick Start

### Prerequisites

- .NET 8 SDK
- Docker & Docker Compose
- Godot 4.3+ (with .NET support)

### Local Development

1. Start infrastructure:
```bash
cd deploy/docker
docker-compose -f docker-compose.dev.yaml up -d
```

2. Run services (in separate terminals):
```bash
# Auth Service
cd src/TowerWars.Auth
dotnet run

# Zone Server
cd src/TowerWars.ZoneServer
dotnet run

# Gateway (optional)
cd src/TowerWars.Gateway
dotnet run
```

3. Open the Godot client:
```bash
cd src/TowerWars.Client
# Open project.godot with Godot Engine
```

### Running Tests

```bash
dotnet test
```

## Project Structure

```
TowerWars/
├── src/
│   ├── TowerWars.Shared/        # DTOs, protocols, constants
│   ├── TowerWars.Auth/          # Authentication service
│   ├── TowerWars.Gateway/       # Entry point, routing
│   ├── TowerWars.WorldManager/  # Zone orchestration
│   ├── TowerWars.ZoneServer/    # Game server (ENet)
│   ├── TowerWars.Social/        # Chat, friends, parties
│   ├── TowerWars.Persistence/   # Event consumer, DB writes
│   └── TowerWars.Client/        # Godot C# client
├── deploy/
│   ├── docker/                  # Docker Compose for dev
│   └── k3s/                     # Kubernetes manifests
└── tests/                       # Unit & integration tests
```

## Services

| Service | Port | Protocol | Description |
|---------|------|----------|-------------|
| Gateway | 7000 | HTTP/WS | Entry point for clients |
| Auth | 7001 | HTTP | Authentication & accounts |
| World Manager | 7002-7003 | HTTP/gRPC | Zone orchestration |
| Zone Server | 7100+ | ENet (UDP) | Game simulation |
| Social | 7004 | HTTP/SignalR | Chat & social features |

## Technology Stack

- **Backend**: .NET 8, ASP.NET Core, Entity Framework
- **Networking**: ENet-CSharp (UDP), SignalR (WebSocket)
- **Serialization**: MessagePack
- **Database**: PostgreSQL
- **Cache/Messaging**: Redis (with Streams)
- **Orchestration**: k3s (Kubernetes)
- **Client**: Godot 4.3 with C#

## k3s Deployment

```bash
# Install k3s
curl -sfL https://get.k3s.io | sh -

# Deploy
kubectl apply -f deploy/k3s/namespace.yaml
kubectl apply -f deploy/k3s/redis/
kubectl apply -f deploy/k3s/postgres/
kubectl apply -f deploy/k3s/auth/
kubectl apply -f deploy/k3s/gateway/
kubectl apply -f deploy/k3s/world-manager/
kubectl apply -f deploy/k3s/zone-server/
kubectl apply -f deploy/k3s/social/
kubectl apply -f deploy/k3s/persistence/
```

## Configuration

Environment variables (or appsettings.json):

```
ConnectionStrings__Postgres=Host=localhost;Database=towerwars;...
ConnectionStrings__Redis=localhost:6379
Jwt__Secret=your-secret-key
Jwt__Issuer=TowerWars.Auth
Jwt__Audience=TowerWars
```

## License

MIT
