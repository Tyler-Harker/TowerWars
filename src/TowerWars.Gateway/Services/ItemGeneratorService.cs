using System.Text.Json;
using TowerWars.Persistence.Data;
using TowerWars.Shared.Constants;

namespace TowerWars.Gateway.Services;

public interface IItemGeneratorService
{
    ItemDrop? TryGenerateItemDrop(Guid userId, string sessionId, int tier, float positionX, float positionY);
}

public class ItemGeneratorService : IItemGeneratorService
{
    private static readonly Random _random = new();

    // Base item templates
    private static readonly string[] WeaponNames = { "Bow", "Crossbow", "Longbow", "Cannon", "Mortar", "Staff", "Wand", "Orb" };
    private static readonly string[] UpgradeNames = { "Scope", "Barrel", "Amplifier", "Core", "Capacitor", "Lens" };

    // Drop rate: base 10% + 2% per tier
    private const float BaseDropRate = 0.10f;
    private const float DropRatePerTier = 0.02f;

    public ItemDrop? TryGenerateItemDrop(Guid userId, string sessionId, int tier, float positionX, float positionY)
    {
        // Calculate drop chance
        var dropChance = BaseDropRate + (tier * DropRatePerTier);
        if (_random.NextDouble() > dropChance)
        {
            return null; // No drop
        }

        var itemLevel = GetItemLevelForTier(tier);
        var rarity = RollRarity();
        var itemType = RollTowerItemType();

        var baseStats = GenerateBaseStats(itemType, itemLevel);
        var affixes = GenerateAffixes(itemLevel, rarity);

        var baseName = GetBaseName(itemType);
        var displayName = BuildDisplayName(baseName, affixes);

        var now = DateTime.UtcNow;

        return new ItemDrop
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionId = sessionId,
            Name = displayName,
            ItemType = itemType,
            Rarity = rarity,
            ItemLevel = itemLevel,
            BaseStatsJson = JsonSerializer.Serialize(baseStats),
            AffixesJson = JsonSerializer.Serialize(affixes),
            PositionX = positionX,
            PositionY = positionY,
            DroppedAt = now,
            ExpiresAt = now.AddMinutes(10), // Items expire after 10 minutes
            IsCollected = false
        };
    }

    private static int GetItemLevelForTier(int tier)
    {
        if (tier <= 1) return 1;
        return _random.Next(1, tier + 1);
    }

    private static TowerItemRarity RollRarity()
    {
        var roll = _random.NextDouble() * 100;

        if (roll < 2) return TowerItemRarity.Legendary;   // 2%
        if (roll < 12) return TowerItemRarity.Rare;       // 10%
        if (roll < 42) return TowerItemRarity.Magic;      // 30%
        return TowerItemRarity.Common;                     // 58%
    }

    private static TowerItemType RollTowerItemType()
    {
        var roll = _random.Next(100);
        if (roll < 50) return TowerItemType.Weapon;
        if (roll < 80) return TowerItemType.TowerUpgrade;
        return TowerItemType.Material;
    }

    private static string GetBaseName(TowerItemType type)
    {
        return type switch
        {
            TowerItemType.Weapon => WeaponNames[_random.Next(WeaponNames.Length)],
            TowerItemType.TowerUpgrade => UpgradeNames[_random.Next(UpgradeNames.Length)],
            TowerItemType.Material => "Essence",
            _ => "Item"
        };
    }

    private static object GenerateBaseStats(TowerItemType type, int itemLevel)
    {
        var multiplier = 1f + (itemLevel - 1) * 0.1f;

        return type switch
        {
            TowerItemType.Weapon => new
            {
                damage = (5 + _random.Next(10)) * multiplier,
                attackSpeed = _random.Next(5, 15) * multiplier * 0.5f,
                critChance = _random.NextDouble() < 0.3 ? _random.NextDouble() * 5 * multiplier * 0.3f : 0
            },
            TowerItemType.TowerUpgrade => new
            {
                range = _random.Next(10, 30) * multiplier * 0.5f,
                attackSpeed = _random.Next(3, 10) * multiplier * 0.3f,
                critDamage = _random.NextDouble() < 0.3 ? _random.Next(10, 30) * multiplier * 0.5f : 0
            },
            _ => new { }
        };
    }

    private static List<object> GenerateAffixes(int itemLevel, TowerItemRarity rarity)
    {
        var affixCount = rarity switch
        {
            TowerItemRarity.Common => 0,
            TowerItemRarity.Magic => _random.Next(1, 3),
            TowerItemRarity.Rare => _random.Next(3, 5),
            TowerItemRarity.Legendary => _random.Next(4, 7),
            _ => 0
        };

        var affixes = new List<object>();
        var maxTier = GetMaxAffixTierForItemLevel(itemLevel);
        var hasPrefix = false;
        var hasSuffix = false;

        for (int i = 0; i < affixCount; i++)
        {
            bool pickPrefix;
            if (!hasPrefix && !hasSuffix)
                pickPrefix = _random.Next(2) == 0;
            else if (!hasPrefix)
                pickPrefix = true;
            else if (!hasSuffix)
                pickPrefix = false;
            else
                pickPrefix = _random.Next(2) == 0;

            var affix = GenerateAffix(itemLevel, maxTier, pickPrefix);
            if (affix != null)
            {
                affixes.Add(affix);
                if (pickPrefix) hasPrefix = true;
                else hasSuffix = true;
            }
        }

        return affixes;
    }

    private static int GetMaxAffixTierForItemLevel(int itemLevel)
    {
        if (itemLevel >= 500) return 5;
        if (itemLevel >= 200) return 4;
        if (itemLevel >= 50) return 3;
        if (itemLevel >= 10) return 2;
        return 1;
    }

    private static object? GenerateAffix(int itemLevel, int maxTier, bool isPrefix)
    {
        var templates = isPrefix ? PrefixTemplates : SuffixTemplates;
        var validTemplates = templates.Where(t => t.Tier <= maxTier).ToList();

        if (validTemplates.Count == 0) return null;

        var template = validTemplates[_random.Next(validTemplates.Count)];
        var baseValue = template.MinValue + (float)(_random.NextDouble() * (template.MaxValue - template.MinValue));
        var levelMultiplier = 1f + (itemLevel - 1) * 0.1f;
        var value = baseValue * levelMultiplier;

        return new
        {
            id = Guid.NewGuid().ToString(),
            name = template.Name,
            isPrefix,
            tier = template.Tier,
            effectType = template.EffectType,
            value
        };
    }

    private static string BuildDisplayName(string baseName, List<object> affixes)
    {
        // For simplicity, just use base name. Full affix names would require more complex logic.
        return baseName;
    }

    // Affix templates
    private static readonly AffixTemplate[] PrefixTemplates =
    {
        new("Sturdy", "FlatDamage", 1, 5, 10),
        new("Quick", "AttackSpeed", 1, 3, 8),
        new("Keen", "CritChance", 1, 1, 3),
        new("Hardened", "FlatDamage", 2, 10, 20),
        new("Swift", "AttackSpeed", 2, 8, 15),
        new("Sharp", "CritChance", 2, 3, 6),
        new("Searing", "BurnDamage", 2, 5, 12),
        new("Tempered", "FlatDamage", 3, 20, 35),
        new("Rapid", "AttackSpeed", 3, 15, 25),
        new("Precise", "CritChance", 3, 6, 10),
        new("Blazing", "BurnDamage", 3, 12, 25),
        new("Arcing", "ChainLightning", 3, 10, 20),
        new("Brutal", "FlatDamage", 4, 35, 55),
        new("Blinding", "AttackSpeed", 4, 25, 40),
        new("Deadly", "CritChance", 4, 10, 15),
        new("Devastating", "FlatDamage", 5, 55, 80),
        new("Godspeed", "AttackSpeed", 5, 40, 60),
        new("Assassin's", "CritChance", 5, 15, 22),
    };

    private static readonly AffixTemplate[] SuffixTemplates =
    {
        new("of Reach", "Range", 1, 10, 20),
        new("of Fortune", "GoldFind", 1, 5, 15),
        new("of Destruction", "CritDamage", 1, 10, 25),
        new("of Distance", "Range", 2, 20, 35),
        new("of Wealth", "GoldFind", 2, 15, 30),
        new("of Ruin", "CritDamage", 2, 25, 45),
        new("of Frost", "SlowOnHit", 2, 10, 20),
        new("of Sniping", "Range", 3, 35, 55),
        new("of Avarice", "GoldFind", 3, 30, 50),
        new("of Annihilation", "CritDamage", 3, 45, 70),
        new("of Winter", "SlowOnHit", 3, 20, 35),
        new("of Piercing", "Pierce", 3, 1, 2),
        new("of the Hawk", "Range", 4, 55, 80),
        new("of the Dragon", "GoldFind", 4, 50, 80),
        new("of Obliteration", "CritDamage", 4, 70, 100),
        new("of the Infinite", "Range", 5, 80, 120),
        new("of the Gods", "GoldFind", 5, 80, 120),
        new("of Extinction", "CritDamage", 5, 100, 150),
    };

    private record AffixTemplate(string Name, string EffectType, int Tier, float MinValue, float MaxValue);
}
