using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using TowerWars.Gateway.Middleware;
using TowerWars.Gateway.Services;
using TowerWars.Persistence.Data;
using TowerWars.Shared.Constants;
using TowerWars.Shared.DTOs;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON to handle circular references
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.AddServiceDefaults();

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// Database
builder.Services.AddDbContext<PersistenceDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
});

// Services
builder.Services.AddSingleton<IZoneRegistryService, ZoneRegistryService>();
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<IItemGeneratorService, ItemGeneratorService>();

// JWT Authentication - validate tokens from OIDC provider
var oidcAuthority = builder.Configuration["Oidc:Authority"] ?? "https://identity.harker.dev/tenant/harker";
var oidcAudience = builder.Configuration["Oidc:ClientId"] ?? "tower-wars-godot";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = oidcAuthority;
        options.Audience = oidcAudience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            // The signing keys are fetched automatically from the OIDC provider's JWKS endpoint
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

// Ensure database schema is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PersistenceDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Ensuring database schema exists...");
    await db.Database.EnsureCreatedAsync();
    logger.LogInformation("Database schema ready");
}

app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiting();

app.MapDefaultEndpoints();

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

// === TOWER ENDPOINTS ===

// Get all towers for current user
app.MapGet("/api/towers", async (HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var towers = await db.PlayerTowers
        .Include(t => t.SkillAllocations)
        .Include(t => t.EquippedItems)
            .ThenInclude(e => e.Item)
        .Where(t => t.UserId == userId.Value)
        .ToListAsync();

    // Create starter towers with equipped weapons for new players
    if (towers.Count == 0)
    {
        var now = DateTime.UtcNow;

        // Create starter towers (stats come from equipped items)
        var bowTower = new PlayerTower
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = "Bow Tower",
            WeaponType = WeaponType.Bow,
            DamageType = DamageType.Physical,
            Level = 1,
            Experience = 0,
            SkillPoints = 0,
            BaseDamage = 0,
            BaseAttackSpeed = 0,
            BaseRange = 0,
            BaseCritChance = 0,
            BaseCritDamage = 150,
            CreatedAt = now,
            UpdatedAt = now
        };

        var cannonTower = new PlayerTower
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = "Cannon Tower",
            WeaponType = WeaponType.Cannon,
            DamageType = DamageType.Physical,
            Level = 1,
            Experience = 0,
            SkillPoints = 0,
            BaseDamage = 0,
            BaseAttackSpeed = 0,
            BaseRange = 0,
            BaseCritChance = 0,
            BaseCritDamage = 150,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Create starter weapon items (ilvl 0, Common rarity)
        var starterBow = new PlayerItem
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = "Starter Bow",
            ItemType = TowerItemType.Weapon,
            Rarity = TowerItemRarity.Common,
            ItemLevel = 0,
            BaseStatsJson = JsonSerializer.Serialize(new
            {
                weaponType = WeaponType.Bow,
                damageType = DamageType.Physical,
                damage = 8,
                attackSpeed = 1.2f,
                range = 150
            }),
            AffixesJson = "[]",
            IsEquipped = true,
            DroppedAt = now,
            CollectedAt = now
        };

        var starterCannon = new PlayerItem
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = "Starter Cannon",
            ItemType = TowerItemType.Weapon,
            Rarity = TowerItemRarity.Common,
            ItemLevel = 0,
            BaseStatsJson = JsonSerializer.Serialize(new
            {
                weaponType = WeaponType.Cannon,
                damageType = DamageType.Physical,
                damage = 20,
                attackSpeed = 0.5f,
                range = 120
            }),
            AffixesJson = "[]",
            IsEquipped = true,
            DroppedAt = now,
            CollectedAt = now
        };

        // Equip weapons to towers
        var bowEquipment = new TowerEquippedItem
        {
            Id = Guid.NewGuid(),
            TowerId = bowTower.Id,
            ItemId = starterBow.Id,
            Slot = "weapon"
        };

        var cannonEquipment = new TowerEquippedItem
        {
            Id = Guid.NewGuid(),
            TowerId = cannonTower.Id,
            ItemId = starterCannon.Id,
            Slot = "weapon"
        };

        bowTower.EquippedItems.Add(bowEquipment);
        cannonTower.EquippedItems.Add(cannonEquipment);

        db.PlayerTowers.AddRange(bowTower, cannonTower);
        db.PlayerItems.AddRange(starterBow, starterCannon);
        await db.SaveChangesAsync();

        towers = new List<PlayerTower> { bowTower, cannonTower };
    }

    return Results.Ok(towers);
}).RequireAuthorization();

// Get single tower
app.MapGet("/api/towers/{towerId:guid}", async (Guid towerId, HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var tower = await db.PlayerTowers
        .Include(t => t.SkillAllocations)
        .Include(t => t.EquippedItems)
            .ThenInclude(e => e.Item)
        .FirstOrDefaultAsync(t => t.Id == towerId && t.UserId == userId.Value);

    if (tower == null) return Results.NotFound();

    return Results.Ok(tower);
}).RequireAuthorization();

// Create new tower (for new players)
app.MapPost("/api/towers", async (CreateTowerRequest request, HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var tower = new PlayerTower
    {
        Id = Guid.NewGuid(),
        UserId = userId.Value,
        Name = request.Name,
        WeaponType = request.WeaponType,
        DamageType = request.DamageType,
        Level = 1,
        Experience = 0,
        SkillPoints = 0,
        BaseDamage = 10,
        BaseAttackSpeed = 1.0f,
        BaseRange = 100,
        BaseCritChance = 0,
        BaseCritDamage = 150,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    db.PlayerTowers.Add(tower);
    await db.SaveChangesAsync();

    return Results.Created($"/api/towers/{tower.Id}", tower);
}).RequireAuthorization();

// Add experience to a tower
app.MapPost("/api/towers/{towerId:guid}/experience", async (Guid towerId, AddExperienceRequest request, HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var tower = await db.PlayerTowers.FirstOrDefaultAsync(t => t.Id == towerId && t.UserId == userId.Value);
    if (tower == null) return Results.NotFound();

    tower.Experience += request.Amount;
    var newLevel = TowerExperience.GetLevelForExperience(tower.Experience);

    // Award skill points for level ups
    if (newLevel > tower.Level)
    {
        var skillPointsToAward = TowerExperience.GetSkillPointsForLevel(newLevel) - TowerExperience.GetSkillPointsForLevel(tower.Level);
        tower.SkillPoints += skillPointsToAward;
        tower.Level = newLevel;
    }

    tower.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(new { tower.Level, tower.Experience, tower.SkillPoints, NextLevelExp = TowerExperience.GetExperienceForNextLevel(tower.Level) });
}).RequireAuthorization();

// Allocate skill points
app.MapPost("/api/towers/{towerId:guid}/skills", async (Guid towerId, AllocateSkillRequest request, HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var tower = await db.PlayerTowers
        .Include(t => t.SkillAllocations)
        .FirstOrDefaultAsync(t => t.Id == towerId && t.UserId == userId.Value);

    if (tower == null) return Results.NotFound();

    // Validate skill exists
    var skillDef = TowerWars.Persistence.Data.SkillTreeDefinitions.AllSkills.FirstOrDefault(s => s.Id == request.SkillId);
    if (skillDef == null) return Results.BadRequest("Invalid skill");

    // Check available skill points
    if (tower.SkillPoints < request.Points) return Results.BadRequest("Not enough skill points");

    // Check prerequisite
    if (!string.IsNullOrEmpty(skillDef.PrerequisiteSkillId))
    {
        var prereq = tower.SkillAllocations.FirstOrDefault(a => a.SkillId == skillDef.PrerequisiteSkillId);
        if (prereq == null || prereq.Points < skillDef.PrerequisitePoints)
            return Results.BadRequest($"Requires {skillDef.PrerequisitePoints} points in {skillDef.PrerequisiteSkillId}");
    }

    var existing = tower.SkillAllocations.FirstOrDefault(a => a.SkillId == request.SkillId);
    if (existing != null)
    {
        if (existing.Points + request.Points > skillDef.MaxPoints)
            return Results.BadRequest($"Max points for this skill is {skillDef.MaxPoints}");

        existing.Points += request.Points;
    }
    else
    {
        if (request.Points > skillDef.MaxPoints)
            return Results.BadRequest($"Max points for this skill is {skillDef.MaxPoints}");

        tower.SkillAllocations.Add(new TowerSkillAllocation
        {
            Id = Guid.NewGuid(),
            TowerId = towerId,
            SkillId = request.SkillId,
            Points = request.Points
        });
    }

    tower.SkillPoints -= request.Points;
    tower.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(tower.SkillAllocations);
}).RequireAuthorization();

// Get skill tree definitions
app.MapGet("/api/skills", () => Results.Ok(TowerWars.Persistence.Data.SkillTreeDefinitions.AllSkills));

// === ITEM ENDPOINTS ===

// Get all items for current user
app.MapGet("/api/items", async (HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var items = await db.PlayerItems
        .Where(i => i.UserId == userId.Value)
        .OrderByDescending(i => i.CollectedAt)
        .ToListAsync();

    return Results.Ok(items);
}).RequireAuthorization();

// Add item to inventory
app.MapPost("/api/items", async (CreateItemRequest request, HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var item = new PlayerItem
    {
        Id = Guid.NewGuid(),
        UserId = userId.Value,
        Name = request.Name,
        ItemType = request.ItemType,
        Rarity = request.Rarity,
        ItemLevel = request.ItemLevel,
        BaseStatsJson = request.BaseStatsJson,
        AffixesJson = request.AffixesJson,
        IsEquipped = false,
        DroppedAt = DateTime.UtcNow,
        CollectedAt = DateTime.UtcNow
    };

    db.PlayerItems.Add(item);
    await db.SaveChangesAsync();

    return Results.Created($"/api/items/{item.Id}", item);
}).RequireAuthorization();

// Equip item to tower
app.MapPost("/api/towers/{towerId:guid}/equip", async (Guid towerId, EquipItemRequest request, HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var tower = await db.PlayerTowers
        .Include(t => t.EquippedItems)
        .FirstOrDefaultAsync(t => t.Id == towerId && t.UserId == userId.Value);

    if (tower == null) return Results.NotFound("Tower not found");

    var item = await db.PlayerItems.FirstOrDefaultAsync(i => i.Id == request.ItemId && i.UserId == userId.Value);
    if (item == null) return Results.NotFound("Item not found");

    // Check if item is already equipped somewhere - unequip it first
    var existingItemEquip = await db.TowerEquippedItems
        .FirstOrDefaultAsync(e => e.ItemId == request.ItemId);
    if (existingItemEquip != null)
    {
        db.TowerEquippedItems.Remove(existingItemEquip);
    }

    // Remove existing item in the target slot
    var existingSlotEquip = tower.EquippedItems.FirstOrDefault(e => e.Slot == request.Slot);
    if (existingSlotEquip != null && existingSlotEquip.ItemId != request.ItemId)
    {
        var oldItem = await db.PlayerItems.FindAsync(existingSlotEquip.ItemId);
        if (oldItem != null) oldItem.IsEquipped = false;
        db.TowerEquippedItems.Remove(existingSlotEquip);
    }

    // Equip new item (only if not already in this slot)
    if (existingSlotEquip?.ItemId != request.ItemId)
    {
        db.TowerEquippedItems.Add(new TowerEquippedItem
        {
            Id = Guid.NewGuid(),
            TowerId = towerId,
            ItemId = request.ItemId,
            Slot = request.Slot
        });
    }

    item.IsEquipped = true;
    tower.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    // Reload equipment for response
    var updatedEquipment = await db.TowerEquippedItems
        .Where(e => e.TowerId == towerId)
        .ToListAsync();

    return Results.Ok(updatedEquipment);
}).RequireAuthorization();

// Unequip item
app.MapPost("/api/towers/{towerId:guid}/unequip", async (Guid towerId, UnequipItemRequest request, HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var tower = await db.PlayerTowers
        .Include(t => t.EquippedItems)
        .FirstOrDefaultAsync(t => t.Id == towerId && t.UserId == userId.Value);

    if (tower == null) return Results.NotFound();

    var equippedItem = tower.EquippedItems.FirstOrDefault(e => e.Slot == request.Slot);
    if (equippedItem == null) return Results.NotFound("No item in that slot");

    var item = await db.PlayerItems.FindAsync(equippedItem.ItemId);
    if (item != null) item.IsEquipped = false;

    db.TowerEquippedItems.Remove(equippedItem);
    tower.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(tower.EquippedItems);
}).RequireAuthorization();

// Delete item (sell/destroy)
app.MapDelete("/api/items/{itemId:guid}", async (Guid itemId, HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var item = await db.PlayerItems.FirstOrDefaultAsync(i => i.Id == itemId && i.UserId == userId.Value);
    if (item == null) return Results.NotFound();

    if (item.IsEquipped) return Results.BadRequest("Cannot delete equipped item");

    db.PlayerItems.Remove(item);
    await db.SaveChangesAsync();

    return Results.Ok();
}).RequireAuthorization();

// === GAME SESSION ENDPOINTS ===

// Report enemy killed - server decides if item drops
app.MapPost("/api/game/enemy-killed", async (EnemyKilledRequest request, HttpContext ctx, PersistenceDbContext db, IItemGeneratorService itemGenerator) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    // Try to generate an item drop
    var itemDrop = itemGenerator.TryGenerateItemDrop(
        userId.Value,
        request.SessionId,
        request.Tier,
        request.PositionX,
        request.PositionY
    );

    if (itemDrop == null)
    {
        // No item dropped
        return Results.Ok(new EnemyKilledResponse(null));
    }

    // Save the item drop to database
    db.ItemDrops.Add(itemDrop);
    await db.SaveChangesAsync();

    return Results.Ok(new EnemyKilledResponse(new ItemDropDto(
        itemDrop.Id,
        itemDrop.Name,
        itemDrop.ItemType,
        itemDrop.Rarity,
        itemDrop.ItemLevel,
        itemDrop.PositionX,
        itemDrop.PositionY
    )));
}).RequireAuthorization();

// Get all pending item drops for a session
app.MapGet("/api/game/item-drops/{sessionId}", async (string sessionId, HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var now = DateTime.UtcNow;
    var drops = await db.ItemDrops
        .Where(d => d.UserId == userId.Value && d.SessionId == sessionId && !d.IsCollected && d.ExpiresAt > now)
        .Select(d => new ItemDropDto(d.Id, d.Name, d.ItemType, d.Rarity, d.ItemLevel, d.PositionX, d.PositionY))
        .ToListAsync();

    return Results.Ok(drops);
}).RequireAuthorization();

// Collect an item drop
app.MapPost("/api/game/item-drops/{dropId:guid}/collect", async (Guid dropId, HttpContext ctx, PersistenceDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var now = DateTime.UtcNow;

    // Find the item drop and verify ownership
    var drop = await db.ItemDrops.FirstOrDefaultAsync(d => d.Id == dropId && d.UserId == userId.Value);

    if (drop == null)
        return Results.NotFound("Item drop not found");

    if (drop.IsCollected)
        return Results.BadRequest("Item already collected");

    if (drop.ExpiresAt <= now)
        return Results.BadRequest("Item drop has expired");

    // Mark as collected
    drop.IsCollected = true;

    // Create the actual item in player's inventory
    var playerItem = new PlayerItem
    {
        Id = Guid.NewGuid(),
        UserId = userId.Value,
        Name = drop.Name,
        ItemType = drop.ItemType,
        Rarity = drop.Rarity,
        ItemLevel = drop.ItemLevel,
        BaseStatsJson = drop.BaseStatsJson,
        AffixesJson = drop.AffixesJson,
        IsEquipped = false,
        DroppedAt = drop.DroppedAt,
        CollectedAt = now
    };

    db.PlayerItems.Add(playerItem);
    await db.SaveChangesAsync();

    return Results.Ok(new CollectItemResponse(playerItem.Id, playerItem.Name, playerItem.Rarity, playerItem.ItemLevel));
}).RequireAuthorization();

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

// Helper function
static Guid? GetUserId(HttpContext ctx)
{
    var userIdClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? ctx.User.FindFirst("sub")?.Value;

    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        return null;

    return userId;
}

// Request DTOs
public record CreateTowerRequest(string Name, WeaponType WeaponType, DamageType DamageType = DamageType.Physical);
public record AddExperienceRequest(long Amount);
public record AllocateSkillRequest(string SkillId, int Points);
public record CreateItemRequest(string Name, TowerItemType ItemType, TowerItemRarity Rarity, int ItemLevel, string BaseStatsJson, string AffixesJson);
public record EquipItemRequest(Guid ItemId, string Slot);
public record UnequipItemRequest(string Slot);

// Game session DTOs
public record EnemyKilledRequest(string SessionId, int Tier, float PositionX, float PositionY);
public record EnemyKilledResponse(ItemDropDto? ItemDrop);
public record ItemDropDto(Guid Id, string Name, TowerItemType ItemType, TowerItemRarity Rarity, int ItemLevel, float PositionX, float PositionY);
public record CollectItemResponse(Guid ItemId, string Name, TowerItemRarity Rarity, int ItemLevel);
