using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TowerWars.Persistence.Data;
using TowerWars.Persistence.Services;

var builder = Host.CreateApplicationBuilder(args);

// Database
builder.Services.AddDbContext<PersistenceDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
});

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// Background service
builder.Services.AddHostedService<EventConsumer>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("TowerWars Persistence Service starting...");

await host.RunAsync();
