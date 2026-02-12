var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var postgres = builder.AddPostgres("postgres-server")
    // .WithDataVolume("towerwars-postgres-data")
    .WithPgAdmin();

// Use plain Redis container without TLS - expose on fixed port
var redis = builder.AddContainer("redis", "redis", "7-alpine")
    // .WithVolume("towerwars-redis-data", "/data")
    .WithEndpoint(targetPort: 6379, port: 6379, name: "tcp");

// Database named "Postgres" to match ConnectionStrings:Postgres
var towerwarsDb = postgres.AddDatabase("Postgres", "towerwars");

// Auth service - depends on postgres and redis
var auth = builder.AddProject<Projects.TowerWars_Auth>("auth")
    .WithReference(towerwarsDb)
    .WithEnvironment("ConnectionStrings__Redis", "localhost:6379")
    .WaitFor(towerwarsDb)
    .WaitFor(redis);

// Gateway - depends on redis, auth, and postgres for tower/item persistence
var gateway = builder.AddProject<Projects.TowerWars_Gateway>("gateway")
    .WithReference(towerwarsDb)
    .WithEnvironment("ConnectionStrings__Redis", "localhost:6379")
    .WithReference(auth)
    .WaitFor(towerwarsDb)
    .WaitFor(redis)
    .WaitFor(auth);

// World Manager - depends on redis
var worldManager = builder.AddProject<Projects.TowerWars_WorldManager>("world-manager")
    .WithEnvironment("ConnectionStrings__Redis", "localhost:6379")
    .WaitFor(redis);

// Zone Server - depends on redis and auth (for token validation)
// Note: ENet UDP server listens on port 7100 (configured via Server:Port)
// UDP ports don't appear in Aspire dashboard - check logs to verify it's running
var zoneServer = builder.AddProject<Projects.TowerWars_ZoneServer>("zone-server")
    .WithEnvironment("ConnectionStrings__Redis", "localhost:6379")
    .WithEnvironment("Server__Port", "7100")
    .WithReference(auth)
    .WaitFor(redis);

// Social - depends on redis and postgres
var social = builder.AddProject<Projects.TowerWars_Social>("social")
    .WithReference(towerwarsDb)
    .WithEnvironment("ConnectionStrings__Redis", "localhost:6379")
    .WaitFor(towerwarsDb)
    .WaitFor(redis);

// Persistence - depends on redis and postgres
var persistence = builder.AddProject<Projects.TowerWars_Persistence>("persistence")
    .WithReference(towerwarsDb)
    .WithEnvironment("ConnectionStrings__Redis", "localhost:6379")
    .WaitFor(towerwarsDb)
    .WaitFor(redis);

// Godot Client - runs the game directly with debug mode for hot reload
var godotPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "godot-mono");
var clientProjectPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "TowerWars.Client"));

var godotClient = builder.AddExecutable("godot-client", godotPath, clientProjectPath, "--debug")
    .WithEnvironment("TOWERWARS_SERVER_ADDRESS", "localhost")
    .WithEnvironment("TOWERWARS_SERVER_PORT", "7100")
    .WithEnvironment("TOWERWARS_GATEWAY_URL", "http://localhost:7000")
    .WithEnvironment("TOWERWARS_AUTO_CONNECT", "false") // Set to "true" to auto-connect on launch
    .WithReference(gateway)
    .WaitFor(gateway)
    .WaitFor(zoneServer);

builder.Build().Run();
