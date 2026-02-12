using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TowerWars.Persistence.Data;
using TowerWars.ZoneServer;
using TowerWars.ZoneServer.Game;
using TowerWars.ZoneServer.Networking;
using TowerWars.ZoneServer.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaultsNonWeb();

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var useRedis = builder.Configuration.GetValue<bool>("UseRedis", true);

if (useRedis)
{
    try
    {
        var redis = ConnectionMultiplexer.Connect(redisConnectionString);
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
        builder.Services.AddSingleton<IEventPublisher, RedisEventPublisher>();
    }
    catch
    {
        Console.WriteLine("Redis not available, using no-op event publisher");
        builder.Services.AddSingleton<IEventPublisher, NoOpEventPublisher>();
    }
}
else
{
    builder.Services.AddSingleton<IEventPublisher, NoOpEventPublisher>();
}

var authServiceUrl = builder.Configuration["AuthService:Url"] ?? "http://localhost:7001";
var useLocalAuth = builder.Configuration.GetValue<bool>("UseLocalAuth", true);

builder.Services.AddHttpClient();

if (useLocalAuth)
{
    builder.Services.AddSingleton<ITokenValidationService, LocalTokenValidationService>();
}
else
{
    builder.Services.AddSingleton<ITokenValidationService>(sp =>
        new TokenValidationService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), authServiceUrl));
}

// Tower bonus service for fetching player bonuses
var useLocalBonuses = builder.Configuration.GetValue<bool>("UseLocalBonuses", true);
if (useLocalBonuses)
{
    builder.Services.AddSingleton<ITowerBonusService, LocalTowerBonusService>();
}
else
{
    builder.Services.AddSingleton<ITowerBonusService>(sp =>
    {
        var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<TowerBonusService>>();
        return new TowerBonusService(httpClient, config, logger);
    });
}

// Database - using IDbContextFactory because ZoneServer services are singletons
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrEmpty(postgresConnectionString))
{
    builder.Services.AddDbContextFactory<PersistenceDbContext>(options =>
    {
        options.UseNpgsql(postgresConnectionString);
    });
    builder.Services.AddSingleton<IPlayerDataService, PlayerDataService>();
}
else
{
    builder.Services.AddSingleton<IPlayerDataService, NoOpPlayerDataService>();
}

builder.Services.AddSingleton<ENetServer>();
builder.Services.AddSingleton<PacketRouter>();
builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<GameSessionManager>();
// Register Lazy<ENetServer> to break circular dependency
builder.Services.AddSingleton(sp => new Lazy<ENetServer>(() => sp.GetRequiredService<ENetServer>()));
builder.Services.AddHostedService<GameLoop>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("TowerWars Zone Server starting...");

await host.RunAsync();
