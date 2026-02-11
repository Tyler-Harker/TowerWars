using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using TowerWars.Gateway.Middleware;
using TowerWars.Gateway.Services;
using TowerWars.Shared.DTOs;

var builder = WebApplication.CreateBuilder(args);

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// Services
builder.Services.AddSingleton<IZoneRegistryService, ZoneRegistryService>();
builder.Services.AddSingleton<ISessionService, SessionService>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TowerWars.Auth",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TowerWars",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiting();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway" }));

// Get zone assignment
app.MapGet("/api/zone/assign", async (
    HttpContext ctx,
    IZoneRegistryService zoneRegistry,
    ISessionService sessionService) =>
{
    var userIdClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? ctx.User.FindFirst("sub")?.Value;

    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var zone = await zoneRegistry.GetAvailableZoneAsync();
    if (zone == null)
        return Results.StatusCode(503);

    var connectionToken = Guid.NewGuid().ToString("N");

    await sessionService.SetSessionAsync(userId, new SessionInfo(
        userId,
        Guid.Empty,
        zone.ZoneId,
        DateTime.UtcNow
    ));

    return Results.Ok(new ZoneAssignment(
        zone.ZoneId,
        zone.Address,
        zone.Port,
        connectionToken,
        DateTime.UtcNow.AddMinutes(5)
    ));
}).RequireAuthorization();

// WebSocket lobby endpoint
app.Map("/ws/lobby", async (HttpContext ctx, ILogger<Program> logger) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = 400;
        return;
    }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    logger.LogInformation("WebSocket lobby connection from {IP}", ctx.Connection.RemoteIpAddress);

    var buffer = new byte[4096];

    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                logger.LogDebug("Received: {Message}", message);

                var response = JsonSerializer.Serialize(new { type = "pong", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await ws.SendAsync(responseBytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
    catch (WebSocketException ex)
    {
        logger.LogWarning(ex, "WebSocket error");
    }
});

// Zone registration (internal endpoint for zone servers)
app.MapPost("/internal/zone/register", async (ZoneInfo zone, IZoneRegistryService zoneRegistry) =>
{
    await zoneRegistry.RegisterZoneAsync(zone);
    return Results.Ok();
});

app.MapPost("/internal/zone/heartbeat", async (string zoneId, int playerCount, IZoneRegistryService zoneRegistry) =>
{
    await zoneRegistry.UpdateZonePlayerCountAsync(zoneId, playerCount);
    return Results.Ok();
});

app.MapDelete("/internal/zone/{zoneId}", async (string zoneId, IZoneRegistryService zoneRegistry) =>
{
    await zoneRegistry.UnregisterZoneAsync(zoneId);
    return Results.Ok();
});

app.Run();
