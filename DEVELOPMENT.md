# TowerWars Development Guide

## Quick Start

### 1. Start the Development Environment

```bash
./dev-start.sh
```

This will:
- Build all Docker images with hot reload support
- Start PostgreSQL and Redis
- Start all 6 microservices
- Configure debugging ports

### 2. Start the Godot Client

```bash
cd src/TowerWars.Client
godot project.godot
```

## Hot Reload

All services are configured with `dotnet watch` for automatic code reloading:

1. Edit any `.cs` file in `src/TowerWars.*`
2. Save the file
3. The service will automatically rebuild and restart

Watch the logs to see the reload happen:
```bash
docker-compose logs -f auth
```

## Debugging in VSCode

### Debug a Single Service

1. Make sure services are running (`./dev-start.sh`)
2. Open VSCode in the project root
3. Press `F5` or go to **Run and Debug**
4. Select the service you want to debug:
   - `Docker: Attach to Auth`
   - `Docker: Attach to Gateway`
   - `Docker: Attach to Zone Server`
   - `Docker: Attach to World Manager`
   - `Docker: Attach to Social`
   - `Docker: Attach to Persistence`
5. Set breakpoints in your code
6. Trigger the code path (e.g., make API call)
7. VSCode will break at your breakpoints!

### Debug Multiple Services

Select **"Debug All Services"** from the debug dropdown to attach debuggers to all services at once.

### Debugging Tips

- **Source Maps**: Already configured to map container paths to your local files
- **Breakpoints**: Just click in the gutter or press `F9`
- **Watch Variables**: Use the Debug sidebar
- **Debug Console**: Evaluate expressions while paused

## Service Ports

### Application Ports
| Service | Port | Protocol |
|---------|------|----------|
| Gateway | 7000 | HTTP |
| Auth | 7001 | HTTP |
| World Manager | 7002 | HTTP |
| World Manager gRPC | 7003 | gRPC |
| Social | 7004 | HTTP/SignalR |
| Zone Server | 7100 | ENet (UDP) |

### Debug Ports
| Service | Debug Port |
|---------|------------|
| Gateway | 5000 |
| Auth | 5001 |
| World Manager | 5002 |
| Zone Server | 5003 |
| Social | 5004 |
| Persistence | 5005 |

### Infrastructure
| Service | Port |
|---------|------|
| PostgreSQL | 5432 |
| Redis | 6379 |

## Useful Commands

### View Logs
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f auth
docker-compose logs -f gateway
docker-compose logs -f zone-server

# Last 100 lines
docker-compose logs --tail=100 auth
```

### Restart a Service
```bash
docker-compose restart auth
```

### Rebuild a Service
```bash
docker-compose up -d --build auth
```

### Stop Everything
```bash
docker-compose down
```

### Stop and Remove Volumes (Clean Slate)
```bash
docker-compose down -v
```

### Access Database
```bash
# Connect to PostgreSQL
docker exec -it towerwars-postgres psql -U dev -d towerwars

# Connect to Redis
docker exec -it towerwars-redis redis-cli
```

## Project Structure

```
TowerWars/
├── src/
│   ├── TowerWars.Auth/          # Authentication service
│   ├── TowerWars.Gateway/       # API Gateway
│   ├── TowerWars.WorldManager/  # World orchestration
│   ├── TowerWars.ZoneServer/    # Game server (ENet)
│   ├── TowerWars.Social/        # Chat & social features
│   ├── TowerWars.Persistence/   # Event consumer
│   ├── TowerWars.Shared/        # Shared DTOs & protocols
│   └── TowerWars.Client/        # Godot game client
├── docker-compose.yml           # Development orchestration
├── dev-start.sh                 # Quick start script
└── .vscode/
    └── launch.json              # Debug configurations
```

## Troubleshooting

### Services won't start
```bash
# Check logs
docker-compose logs

# Rebuild from scratch
docker-compose down -v
docker-compose up --build
```

### Hot reload not working
1. Check that `DOTNET_USE_POLLING_FILE_WATCHER=true` is set
2. Ensure volumes are mounted correctly in `docker-compose.yml`
3. Check logs: `docker-compose logs -f [service]`

### Can't attach debugger
1. Ensure service is running: `docker-compose ps`
2. Check if vsdbg is installed: `docker exec towerwars-auth ls /vsdbg`
3. Rebuild the service: `docker-compose up -d --build [service]`

### Database connection issues
```bash
# Check if PostgreSQL is ready
docker exec towerwars-postgres pg_isready -U dev

# View PostgreSQL logs
docker-compose logs postgres
```

## VSCode Extensions (Recommended)

- **C# Dev Kit** - C# language support
- **C#** - IntelliSense and debugging
- **Docker** - Docker file support
- **Remote - Containers** - Container development

## Next Steps

- Read the main [README.md](README.md) for architecture overview
- Check out [src/TowerWars.Client/](src/TowerWars.Client/) for client code
- Review the game design docs (coming soon)
