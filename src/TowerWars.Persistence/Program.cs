using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TowerWars.Persistence.Data;
using TowerWars.Persistence.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaultsNonWeb();

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

// Ensure database schema is created
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PersistenceDbContext>();
    logger.LogInformation("Ensuring database schema exists...");
    await db.Database.EnsureCreatedAsync();
    logger.LogInformation("Database schema ready");
}

await host.RunAsync();
