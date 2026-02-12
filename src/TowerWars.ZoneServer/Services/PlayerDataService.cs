using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TowerWars.Persistence.Data;
using TowerWars.Shared.Constants;
using TowerWars.Shared.Protocol;

namespace TowerWars.ZoneServer.Services;

public interface IPlayerDataService
{
    Task<(WirePlayerTower[] Towers, WirePlayerItem[] Items)> GetPlayerDataAsync(Guid userId);
}

public sealed class PlayerDataService : IPlayerDataService
{
    private readonly IDbContextFactory<PersistenceDbContext> _dbFactory;
    private readonly ILogger<PlayerDataService> _logger;

    public PlayerDataService(
        IDbContextFactory<PersistenceDbContext> dbFactory,
        ILogger<PlayerDataService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<(WirePlayerTower[] Towers, WirePlayerItem[] Items)> GetPlayerDataAsync(Guid userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var towers = await db.PlayerTowers
            .Include(t => t.SkillAllocations)
            .Include(t => t.EquippedItems)
                .ThenInclude(e => e.Item)
            .Where(t => t.UserId == userId)
            .ToListAsync();

        if (towers.Count == 0)
        {
            towers = await CreateStarterTowersAsync(db, userId);
        }

        var items = await db.PlayerItems
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CollectedAt)
            .ToListAsync();

        var wireTowers = towers.Select(MapTower).ToArray();
        var wireItems = items.Select(MapItem).ToArray();

        _logger.LogDebug("Loaded player data for {UserId}: {TowerCount} towers, {ItemCount} items",
            userId, wireTowers.Length, wireItems.Length);

        return (wireTowers, wireItems);
    }

    private static WirePlayerTower MapTower(PlayerTower t) => new()
    {
        Id = t.Id,
        UserId = t.UserId,
        Name = t.Name,
        WeaponType = (byte)t.WeaponType,
        DamageType = (byte)t.DamageType,
        Level = t.Level,
        Experience = t.Experience,
        SkillPoints = t.SkillPoints,
        BaseDamage = t.BaseDamage,
        BaseAttackSpeed = t.BaseAttackSpeed,
        BaseRange = t.BaseRange,
        BaseCritChance = t.BaseCritChance,
        BaseCritDamage = t.BaseCritDamage,
        SkillAllocations = t.SkillAllocations.Select(s => new WireTowerSkillAllocation
        {
            Id = s.Id,
            TowerId = s.TowerId,
            SkillId = s.SkillId,
            Points = s.Points
        }).ToArray(),
        EquippedItems = t.EquippedItems.Select(e => new WireTowerEquippedItem
        {
            Id = e.Id,
            TowerId = e.TowerId,
            ItemId = e.ItemId,
            Slot = e.Slot,
            Item = e.Item != null ? MapItem(e.Item) : null
        }).ToArray()
    };

    private static WirePlayerItem MapItem(PlayerItem i) => new()
    {
        Id = i.Id,
        UserId = i.UserId,
        Name = i.Name,
        ItemType = (byte)i.ItemType,
        Rarity = (byte)i.Rarity,
        ItemLevel = i.ItemLevel,
        BaseStatsJson = i.BaseStatsJson,
        AffixesJson = i.AffixesJson,
        IsEquipped = i.IsEquipped,
        DroppedAtUnixMs = new DateTimeOffset(i.DroppedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        CollectedAtUnixMs = i.CollectedAt.HasValue
            ? new DateTimeOffset(i.CollectedAt.Value, TimeSpan.Zero).ToUnixTimeMilliseconds()
            : null
    };

    private async Task<List<PlayerTower>> CreateStarterTowersAsync(PersistenceDbContext db, Guid userId)
    {
        var now = DateTime.UtcNow;

        var bowTower = new PlayerTower
        {
            Id = Guid.NewGuid(),
            UserId = userId,
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
            UserId = userId,
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

        var starterBow = new PlayerItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
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
            UserId = userId,
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

        _logger.LogInformation("Created starter towers for new player {UserId}", userId);

        return [bowTower, cannonTower];
    }
}

public sealed class NoOpPlayerDataService : IPlayerDataService
{
    public Task<(WirePlayerTower[] Towers, WirePlayerItem[] Items)> GetPlayerDataAsync(Guid userId)
    {
        return Task.FromResult((Array.Empty<WirePlayerTower>(), Array.Empty<WirePlayerItem>()));
    }
}
