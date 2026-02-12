using StackExchange.Redis;
using TowerWars.Shared.DTOs;
using TowerWars.Shared.Protocol;
using TowerWars.WorldManager.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// gRPC
builder.Services.AddGrpc();

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// Services
builder.Services.AddSingleton<IZoneOrchestrator, ZoneOrchestrator>();
builder.Services.AddSingleton<IMatchmakingService, MatchmakingService>();

// Background matchmaking processor
builder.Services.AddHostedService<MatchmakingBackgroundService>();

var app = builder.Build();

// gRPC endpoint
app.MapGrpcService<WorldGrpcService>();

app.MapDefaultEndpoints();

// REST API endpoints (for simpler integrations)
app.MapGet("/api/zones", async (IZoneOrchestrator orchestrator) =>
{
    var zones = await orchestrator.GetAllZonesAsync();
    return Results.Ok(zones);
});

app.MapGet("/api/zones/{zoneId}", async (string zoneId, IZoneOrchestrator orchestrator) =>
{
    var zone = await orchestrator.GetZoneAsync(zoneId);
    return zone != null ? Results.Ok(zone) : Results.NotFound();
});

app.MapPost("/api/matchmaking/enqueue", async (
    MatchmakingRequest request,
    IMatchmakingService matchmaking,
    HttpContext ctx) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
    {
        userId = Guid.NewGuid();
    }

    var ticketId = await matchmaking.EnqueueAsync(userId, request.Mode, 1000);

    return Results.Ok(new MatchmakingResponse(
        true,
        ticketId,
        30,
        null
    ));
});

app.MapGet("/api/matchmaking/{ticketId}", async (Guid ticketId, IMatchmakingService matchmaking) =>
{
    var status = await matchmaking.GetStatusAsync(ticketId);
    return Results.Ok(status);
});

app.MapDelete("/api/matchmaking/{ticketId}", async (Guid ticketId, IMatchmakingService matchmaking) =>
{
    await matchmaking.CancelAsync(ticketId);
    return Results.Ok();
});

app.Run();

public sealed class MatchmakingBackgroundService : BackgroundService
{
    private readonly IMatchmakingService _matchmaking;
    private readonly ILogger<MatchmakingBackgroundService> _logger;

    public MatchmakingBackgroundService(
        IMatchmakingService matchmaking,
        ILogger<MatchmakingBackgroundService> logger)
    {
        _matchmaking = matchmaking;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _matchmaking.ProcessQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing matchmaking queue");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
