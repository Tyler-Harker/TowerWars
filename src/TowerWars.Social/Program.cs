using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using TowerWars.Shared.DTOs;
using TowerWars.Social.Data;
using TowerWars.Social.Hubs;
using TowerWars.Social.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<SocialDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
});

// Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnectionString));

// SignalR with Redis backplane
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("social:");
    });

// Services
builder.Services.AddScoped<IPresenceService, PresenceService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<IPartyService, PartyService>();

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
            ValidateLifetime = true
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// SignalR hub
app.MapHub<SocialHub>("/hubs/social");

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "social" }));

// REST endpoints
app.MapGet("/api/friends", async (HttpContext ctx, IFriendService friends) =>
{
    var userId = GetUserId(ctx);
    if (!userId.HasValue) return Results.Unauthorized();

    var result = await friends.GetFriendsAsync(userId.Value);
    return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/friends/request/{friendId:guid}", async (Guid friendId, HttpContext ctx, IFriendService friends) =>
{
    var userId = GetUserId(ctx);
    if (!userId.HasValue) return Results.Unauthorized();

    var success = await friends.SendFriendRequestAsync(userId.Value, friendId);
    return success ? Results.Ok() : Results.BadRequest();
}).RequireAuthorization();

app.MapPost("/api/friends/accept/{friendId:guid}", async (Guid friendId, HttpContext ctx, IFriendService friends) =>
{
    var userId = GetUserId(ctx);
    if (!userId.HasValue) return Results.Unauthorized();

    var success = await friends.AcceptFriendRequestAsync(userId.Value, friendId);
    return success ? Results.Ok() : Results.BadRequest();
}).RequireAuthorization();

app.MapDelete("/api/friends/{friendId:guid}", async (Guid friendId, HttpContext ctx, IFriendService friends) =>
{
    var userId = GetUserId(ctx);
    if (!userId.HasValue) return Results.Unauthorized();

    var success = await friends.RemoveFriendAsync(userId.Value, friendId);
    return success ? Results.Ok() : Results.NotFound();
}).RequireAuthorization();

app.MapGet("/api/party", async (HttpContext ctx, IPartyService parties) =>
{
    var userId = GetUserId(ctx);
    if (!userId.HasValue) return Results.Unauthorized();

    var party = await parties.GetUserPartyAsync(userId.Value);
    return party != null ? Results.Ok(party) : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/api/party", async (HttpContext ctx, IPartyService parties) =>
{
    var userId = GetUserId(ctx);
    if (!userId.HasValue) return Results.Unauthorized();

    var party = await parties.CreatePartyAsync(userId.Value);
    return party != null ? Results.Created($"/api/party/{party.PartyId}", party) : Results.BadRequest();
}).RequireAuthorization();

app.MapPost("/api/party/{partyId:guid}/join", async (Guid partyId, HttpContext ctx, IPartyService parties) =>
{
    var userId = GetUserId(ctx);
    if (!userId.HasValue) return Results.Unauthorized();

    var success = await parties.JoinPartyAsync(partyId, userId.Value);
    return success ? Results.Ok() : Results.BadRequest();
}).RequireAuthorization();

app.MapPost("/api/party/leave", async (HttpContext ctx, IPartyService parties) =>
{
    var userId = GetUserId(ctx);
    if (!userId.HasValue) return Results.Unauthorized();

    var success = await parties.LeavePartyAsync(userId.Value);
    return success ? Results.Ok() : Results.BadRequest();
}).RequireAuthorization();

app.Run();

static Guid? GetUserId(HttpContext ctx)
{
    var claim = ctx.User.FindFirst("sub")?.Value;
    return Guid.TryParse(claim, out var userId) ? userId : null;
}
