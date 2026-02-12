using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using TowerWars.Auth.Data;
using TowerWars.Auth.Services;
using TowerWars.Shared.Constants;
using TowerWars.Shared.DTOs;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configuration
var jwtSettings = new JwtSettings
{
    Secret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured"),
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "TowerWars.Auth",
    Audience = builder.Configuration["Jwt:Audience"] ?? "TowerWars",
    AccessTokenExpirationMinutes = int.Parse(builder.Configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60"),
    RefreshTokenExpirationDays = int.Parse(builder.Configuration["Jwt:RefreshTokenExpirationDays"] ?? "7")
};

// Database
builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
});

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// Services
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISessionCacheService, SessionCacheService>();
builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<IConnectionTokenService, ConnectionTokenService>();
builder.Services.AddScoped<ITowerProgressionService, TowerProgressionService>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();

// Background services
builder.Services.AddHostedService<TowerXpConsumer>();

// Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = builder.Configuration["Oidc:Authority"] ?? "https://identity.harker.dev/tenant/harker";
        options.ClientId = builder.Configuration["Oidc:ClientId"] ?? "tower-wars-godot";
        options.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        // Map OIDC claims to standard claims
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure database schema is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Ensuring Auth database schema exists...");
    await db.Database.EnsureCreatedAsync();
    logger.LogInformation("Auth database schema ready");
}

// Middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

// Auth endpoints
app.MapPost("/api/auth/register", async (RegisterRequest request, IAuthService authService, HttpContext ctx) =>
{
    var ipAddress = ctx.Connection.RemoteIpAddress?.ToString();
    var result = await authService.RegisterAsync(request, ipAddress);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/auth/login", async (LoginRequest request, IAuthService authService, HttpContext ctx) =>
{
    var ipAddress = ctx.Connection.RemoteIpAddress?.ToString();
    var result = await authService.LoginAsync(request, ipAddress);
    return result.Success ? Results.Ok(result) : Results.Unauthorized();
});

app.MapPost("/api/auth/refresh", async (RefreshTokenRequest request, IAuthService authService, HttpContext ctx) =>
{
    var ipAddress = ctx.Connection.RemoteIpAddress?.ToString();
    var result = await authService.RefreshTokenAsync(request.RefreshToken, ipAddress);
    return result.Success ? Results.Ok(result) : Results.Unauthorized();
});

app.MapPost("/api/auth/logout", async (HttpContext ctx, IAuthService authService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    await authService.LogoutAsync(userId);
    return Results.Ok();
}).RequireAuthorization();

// Character endpoints
app.MapGet("/api/characters", async (HttpContext ctx, ICharacterService characterService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var result = await characterService.GetCharactersAsync(userId);
    return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/characters", async (CreateCharacterRequest request, HttpContext ctx, ICharacterService characterService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var result = await characterService.CreateCharacterAsync(userId, request);
    return result != null ? Results.Created($"/api/characters/{result.Id}", result) : Results.BadRequest();
}).RequireAuthorization();

app.MapDelete("/api/characters/{id:guid}", async (Guid id, HttpContext ctx, ICharacterService characterService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var success = await characterService.DeleteCharacterAsync(userId, id);
    return success ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/api/characters/{id:guid}/select", async (Guid id, HttpContext ctx, ICharacterService characterService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var result = await characterService.SelectCharacterAsync(userId, id);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).RequireAuthorization();

// Internal endpoint for other services to validate tokens
app.MapPost("/internal/validate-connection-token", async (string token, IConnectionTokenService tokenService) =>
{
    var result = await tokenService.ValidateTokenAsync(token);
    if (result == null)
        return Results.NotFound();

    return Results.Ok(new { userId = result.Value.UserId, characterId = result.Value.CharacterId });
});

// Tower progression endpoints
app.MapGet("/api/towers", async (HttpContext ctx, ITowerProgressionService progressionService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var result = await progressionService.GetPlayerTowersAsync(userId);
    return Results.Ok(result);
}).RequireAuthorization();

app.MapGet("/api/towers/{towerId:guid}", async (Guid towerId, HttpContext ctx, ITowerProgressionService progressionService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out _))
        return Results.Unauthorized();

    var result = await progressionService.GetTowerDetailAsync(towerId);
    return result != null ? Results.Ok(result) : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/api/towers/allocate-skill", async (AllocateSkillRequest request, HttpContext ctx, ITowerProgressionService progressionService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var result = await progressionService.AllocateSkillPointAsync(userId, request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).RequireAuthorization();

app.MapPost("/api/towers/reset-skills", async (ResetSkillsRequest request, HttpContext ctx, ITowerProgressionService progressionService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var result = await progressionService.ResetSkillsAsync(userId, request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).RequireAuthorization();

// Equipment endpoints
app.MapGet("/api/inventory", async (int page, int pageSize, HttpContext ctx, IEquipmentService equipmentService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 10, 100);

    var result = await equipmentService.GetInventoryAsync(userId, page, pageSize);
    return Results.Ok(result);
}).RequireAuthorization();

app.MapGet("/api/towers/{towerId:guid}/equipment", async (Guid towerId, HttpContext ctx, IEquipmentService equipmentService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out _))
        return Results.Unauthorized();

    var result = await equipmentService.GetTowerEquipmentAsync(towerId);
    return result != null ? Results.Ok(result) : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/api/equipment/equip", async (EquipItemRequest request, HttpContext ctx, IEquipmentService equipmentService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var result = await equipmentService.EquipItemAsync(userId, request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).RequireAuthorization();

app.MapPost("/api/equipment/unequip", async (UnequipItemRequest request, HttpContext ctx, IEquipmentService equipmentService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var result = await equipmentService.UnequipItemAsync(userId, request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).RequireAuthorization();

app.MapDelete("/api/inventory/{itemId:guid}", async (Guid itemId, HttpContext ctx, IEquipmentService equipmentService) =>
{
    var userIdClaim = ctx.User.FindFirst("sub")?.Value;
    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var result = await equipmentService.DeleteItemAsync(userId, new DeleteItemRequest(itemId));
    return result.Success ? Results.NoContent() : Results.BadRequest(result);
}).RequireAuthorization();

// Internal endpoints for zone server
app.MapGet("/internal/towers/{towerId:guid}/bonuses", async (
    Guid towerId,
    ITowerProgressionService progressionService,
    IEquipmentService equipmentService) =>
{
    var skillBonuses = await progressionService.GetTowerBonusesAsync(towerId);
    var equipmentBonuses = await equipmentService.GetEquipmentBonusesAsync(towerId);

    // Merge bonuses
    var allBonuses = new Dictionary<TowerBonusType, decimal>(skillBonuses.AllBonuses);
    foreach (var kvp in equipmentBonuses.AllBonuses)
    {
        if (allBonuses.ContainsKey(kvp.Key))
            allBonuses[kvp.Key] += kvp.Value;
        else
            allBonuses[kvp.Key] = kvp.Value;
    }

    return Results.Ok(new TowerBonusSummaryDto(
        allBonuses.GetValueOrDefault(TowerBonusType.DamagePercent, 0),
        allBonuses.GetValueOrDefault(TowerBonusType.DamageFlat, 0),
        allBonuses.GetValueOrDefault(TowerBonusType.AttackSpeedPercent, 0),
        allBonuses.GetValueOrDefault(TowerBonusType.RangePercent, 0),
        allBonuses.GetValueOrDefault(TowerBonusType.CritChance, 0),
        allBonuses.GetValueOrDefault(TowerBonusType.CritMultiplier, 0),
        allBonuses.GetValueOrDefault(TowerBonusType.TowerHpFlat, 0),
        allBonuses.GetValueOrDefault(TowerBonusType.TowerHpPercent, 0),
        allBonuses.GetValueOrDefault(TowerBonusType.DamageReductionPercent, 0),
        allBonuses.GetValueOrDefault(TowerBonusType.GoldFindPercent, 0),
        allBonuses.GetValueOrDefault(TowerBonusType.XpGainPercent, 0),
        allBonuses
    ));
});

app.MapGet("/internal/towers/{towerId:guid}/equipment/weapon", async (
    Guid towerId,
    IEquipmentService equipmentService) =>
{
    var equipment = await equipmentService.GetTowerEquipmentAsync(towerId);
    var weapon = equipment?.Equipment.FirstOrDefault(e => e.Slot == EquipmentSlot.Weapon)?.Item;

    if (weapon?.Base.WeaponSubtype == null)
        return Results.NotFound();

    var attackStyle = new WeaponAttackStyleDto(
        weapon.Base.WeaponSubtype.Value,
        weapon.Base.BaseDamage ?? 0,
        weapon.Base.BaseRange ?? 0,
        weapon.Base.BaseAttackSpeed ?? 0,
        weapon.Base.HitsMultiple,
        weapon.Base.MaxTargets,
        weapon.Base.WeaponSubtype != WeaponSubtype.Sword && weapon.Base.WeaponSubtype != WeaponSubtype.Club,
        weapon.Base.WeaponSubtype == WeaponSubtype.Axe
    );

    return Results.Ok(attackStyle);
});

app.Run();
