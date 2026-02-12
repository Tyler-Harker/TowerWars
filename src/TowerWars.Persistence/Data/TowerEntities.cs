using TowerWars.Shared.Constants;

namespace TowerWars.Persistence.Data;

/// <summary>
/// Represents a tower owned by a player. Towers gain experience and have skill trees.
/// </summary>
public class PlayerTower
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public WeaponType WeaponType { get; set; } = WeaponType.Bow;
    public DamageType DamageType { get; set; } = DamageType.Physical;
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public int SkillPoints { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Base stats (can be modified by level)
    public float BaseDamage { get; set; } = 10;
    public float BaseAttackSpeed { get; set; } = 1.0f;
    public float BaseRange { get; set; } = 100;
    public float BaseCritChance { get; set; }
    public float BaseCritDamage { get; set; } = 150; // 150% = 1.5x

    public ICollection<TowerSkillAllocation> SkillAllocations { get; set; } = [];
    public ICollection<TowerEquippedItem> EquippedItems { get; set; } = [];
}

/// <summary>
/// Skill allocation for a specific tower
/// </summary>
public class TowerSkillAllocation
{
    public Guid Id { get; set; }
    public Guid TowerId { get; set; }
    public string SkillId { get; set; } = "";
    public int Points { get; set; }

    public PlayerTower? Tower { get; set; }
}

/// <summary>
/// Item equipped on a tower
/// </summary>
public class TowerEquippedItem
{
    public Guid Id { get; set; }
    public Guid TowerId { get; set; }
    public Guid ItemId { get; set; }
    public string Slot { get; set; } = ""; // weapon, upgrade1, upgrade2, etc.

    public PlayerTower? Tower { get; set; }
    public PlayerItem? Item { get; set; }
}

/// <summary>
/// An item owned by a player
/// </summary>
public class PlayerItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public TowerItemType ItemType { get; set; } = TowerItemType.Weapon;
    public TowerItemRarity Rarity { get; set; } = TowerItemRarity.Common;
    public int ItemLevel { get; set; }
    public string BaseStatsJson { get; set; } = "{}"; // JSON blob of base stats
    public string AffixesJson { get; set; } = "[]"; // JSON blob of affixes
    public bool IsEquipped { get; set; }
    public DateTime DroppedAt { get; set; }
    public DateTime? CollectedAt { get; set; }
}

/// <summary>
/// An item drop that exists in the game world, waiting to be collected
/// </summary>
public class ItemDrop
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SessionId { get; set; } = ""; // Game session identifier
    public string Name { get; set; } = "";
    public TowerItemType ItemType { get; set; } = TowerItemType.Weapon;
    public TowerItemRarity Rarity { get; set; } = TowerItemRarity.Common;
    public int ItemLevel { get; set; }
    public string BaseStatsJson { get; set; } = "{}";
    public string AffixesJson { get; set; } = "[]";
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public DateTime DroppedAt { get; set; }
    public DateTime ExpiresAt { get; set; } // Item drops expire after some time
    public bool IsCollected { get; set; }
}

/// <summary>
/// Defines a skill in the tower skill tree
/// </summary>
public class SkillDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = ""; // Offense, Defense, Utility
    public int MaxPoints { get; set; } = 5;
    public string? PrerequisiteSkillId { get; set; }
    public int PrerequisitePoints { get; set; }
    public string EffectType { get; set; } = ""; // PercentDamage, FlatDamage, CritChance, etc.
    public float EffectValuePerPoint { get; set; }
    public DamageType? DamageType { get; set; } // null = all, or specific type
    public int TierRequired { get; set; } = 1; // Minimum tier unlocked to access
}

/// <summary>
/// Experience thresholds for tower levels
/// </summary>
public static class TowerExperience
{
    // Experience required to reach each level (cumulative)
    public static readonly long[] LevelThresholds =
    {
        0,          // Level 1
        100,        // Level 2
        300,        // Level 3
        600,        // Level 4
        1000,       // Level 5
        1500,       // Level 6
        2200,       // Level 7
        3100,       // Level 8
        4200,       // Level 9
        5500,       // Level 10
        7000,       // Level 11
        8800,       // Level 12
        10900,      // Level 13
        13300,      // Level 14
        16100,      // Level 15
        19400,      // Level 16
        23200,      // Level 17
        27600,      // Level 18
        32700,      // Level 19
        38500,      // Level 20 (max for now)
    };

    public const int MaxLevel = 20;

    public static int GetLevelForExperience(long experience)
    {
        for (int i = LevelThresholds.Length - 1; i >= 0; i--)
        {
            if (experience >= LevelThresholds[i])
                return i + 1;
        }
        return 1;
    }

    public static long GetExperienceForNextLevel(int currentLevel)
    {
        if (currentLevel >= MaxLevel) return 0;
        return LevelThresholds[currentLevel];
    }

    public static int GetSkillPointsForLevel(int level)
    {
        // 1 skill point per level starting at level 2
        return Math.Max(0, level - 1);
    }
}

/// <summary>
/// Predefined skill tree definitions
/// </summary>
public static class SkillTreeDefinitions
{
    public static readonly SkillDefinition[] AllSkills = new[]
    {
        // === OFFENSE TREE ===
        // Tier 1 - Basic damage
        new SkillDefinition
        {
            Id = "increased_damage",
            Name = "Increased Damage",
            Description = "Increases all damage dealt by {0}%",
            Category = "Offense",
            MaxPoints = 10,
            EffectType = "PercentDamage",
            EffectValuePerPoint = 3f, // 3% per point, max 30%
            TierRequired = 1
        },
        new SkillDefinition
        {
            Id = "increased_attack_speed",
            Name = "Increased Attack Speed",
            Description = "Increases attack speed by {0}%",
            Category = "Offense",
            MaxPoints = 10,
            EffectType = "PercentAttackSpeed",
            EffectValuePerPoint = 2f, // 2% per point, max 20%
            TierRequired = 1
        },

        // Tier 2 - Critical strikes
        new SkillDefinition
        {
            Id = "critical_strike_chance",
            Name = "Critical Strike Chance",
            Description = "Increases critical strike chance by {0}%",
            Category = "Offense",
            MaxPoints = 8,
            PrerequisiteSkillId = "increased_damage",
            PrerequisitePoints = 3,
            EffectType = "FlatCritChance",
            EffectValuePerPoint = 1.5f, // 1.5% per point, max 12%
            TierRequired = 2
        },
        new SkillDefinition
        {
            Id = "critical_strike_damage",
            Name = "Critical Strike Damage",
            Description = "Increases critical strike damage by {0}%",
            Category = "Offense",
            MaxPoints = 8,
            PrerequisiteSkillId = "critical_strike_chance",
            PrerequisitePoints = 2,
            EffectType = "PercentCritDamage",
            EffectValuePerPoint = 10f, // 10% per point, max 80%
            TierRequired = 2
        },

        // Tier 3 - Elemental damage
        new SkillDefinition
        {
            Id = "physical_damage",
            Name = "Physical Mastery",
            Description = "Increases Physical damage by {0}%",
            Category = "Offense",
            MaxPoints = 10,
            PrerequisiteSkillId = "increased_damage",
            PrerequisitePoints = 5,
            EffectType = "PercentDamage",
            EffectValuePerPoint = 5f,
            DamageType = DamageType.Physical,
            TierRequired = 3
        },
        new SkillDefinition
        {
            Id = "fire_damage",
            Name = "Fire Mastery",
            Description = "Increases Fire damage by {0}%",
            Category = "Offense",
            MaxPoints = 10,
            PrerequisiteSkillId = "increased_damage",
            PrerequisitePoints = 5,
            EffectType = "PercentDamage",
            EffectValuePerPoint = 5f,
            DamageType = DamageType.Fire,
            TierRequired = 3
        },
        new SkillDefinition
        {
            Id = "cold_damage",
            Name = "Cold Mastery",
            Description = "Increases Cold damage by {0}%",
            Category = "Offense",
            MaxPoints = 10,
            PrerequisiteSkillId = "increased_damage",
            PrerequisitePoints = 5,
            EffectType = "PercentDamage",
            EffectValuePerPoint = 5f,
            DamageType = DamageType.Cold,
            TierRequired = 3
        },
        new SkillDefinition
        {
            Id = "lightning_damage",
            Name = "Lightning Mastery",
            Description = "Increases Lightning damage by {0}%",
            Category = "Offense",
            MaxPoints = 10,
            PrerequisiteSkillId = "increased_damage",
            PrerequisitePoints = 5,
            EffectType = "PercentDamage",
            EffectValuePerPoint = 5f,
            DamageType = DamageType.Lightning,
            TierRequired = 3
        },
        new SkillDefinition
        {
            Id = "chaos_damage",
            Name = "Chaos Mastery",
            Description = "Increases Chaos damage by {0}%",
            Category = "Offense",
            MaxPoints = 10,
            PrerequisiteSkillId = "increased_damage",
            PrerequisitePoints = 5,
            EffectType = "PercentDamage",
            EffectValuePerPoint = 5f,
            DamageType = DamageType.Chaos,
            TierRequired = 4
        },
        new SkillDefinition
        {
            Id = "holy_damage",
            Name = "Holy Mastery",
            Description = "Increases Holy damage by {0}%",
            Category = "Offense",
            MaxPoints = 10,
            PrerequisiteSkillId = "increased_damage",
            PrerequisitePoints = 5,
            EffectType = "PercentDamage",
            EffectValuePerPoint = 5f,
            DamageType = DamageType.Holy,
            TierRequired = 4
        },

        // Tier 4 - Ailments
        new SkillDefinition
        {
            Id = "ignite_chance",
            Name = "Ignite Chance",
            Description = "Gain {0}% chance to ignite enemies on hit",
            Category = "Offense",
            MaxPoints = 5,
            PrerequisiteSkillId = "fire_damage",
            PrerequisitePoints = 3,
            EffectType = "IgniteChance",
            EffectValuePerPoint = 4f, // 4% per point, max 20%
            TierRequired = 4
        },
        new SkillDefinition
        {
            Id = "freeze_chance",
            Name = "Freeze Chance",
            Description = "Gain {0}% chance to freeze enemies on hit",
            Category = "Offense",
            MaxPoints = 5,
            PrerequisiteSkillId = "cold_damage",
            PrerequisitePoints = 3,
            EffectType = "FreezeChance",
            EffectValuePerPoint = 3f, // 3% per point, max 15%
            TierRequired = 4
        },
        new SkillDefinition
        {
            Id = "shock_chance",
            Name = "Shock Chance",
            Description = "Gain {0}% chance to shock enemies on hit",
            Category = "Offense",
            MaxPoints = 5,
            PrerequisiteSkillId = "lightning_damage",
            PrerequisitePoints = 3,
            EffectType = "ShockChance",
            EffectValuePerPoint = 4f,
            TierRequired = 4
        },

        // === DEFENSE TREE ===
        new SkillDefinition
        {
            Id = "increased_range",
            Name = "Increased Range",
            Description = "Increases attack range by {0}%",
            Category = "Defense",
            MaxPoints = 8,
            EffectType = "PercentRange",
            EffectValuePerPoint = 4f, // 4% per point, max 32%
            TierRequired = 1
        },
        new SkillDefinition
        {
            Id = "pierce",
            Name = "Piercing Shots",
            Description = "Projectiles pierce through {0} additional enemies",
            Category = "Defense",
            MaxPoints = 3,
            PrerequisiteSkillId = "increased_range",
            PrerequisitePoints = 3,
            EffectType = "Pierce",
            EffectValuePerPoint = 1f,
            TierRequired = 3
        },
        new SkillDefinition
        {
            Id = "chain",
            Name = "Chain",
            Description = "Projectiles chain to {0} additional enemies",
            Category = "Defense",
            MaxPoints = 3,
            PrerequisiteSkillId = "increased_range",
            PrerequisitePoints = 5,
            EffectType = "Chain",
            EffectValuePerPoint = 1f,
            TierRequired = 4
        },

        // === UTILITY TREE ===
        new SkillDefinition
        {
            Id = "gold_find",
            Name = "Gold Find",
            Description = "Increases gold dropped by killed enemies by {0}%",
            Category = "Utility",
            MaxPoints = 10,
            EffectType = "GoldFind",
            EffectValuePerPoint = 5f, // 5% per point, max 50%
            TierRequired = 1
        },
        new SkillDefinition
        {
            Id = "item_find",
            Name = "Item Find",
            Description = "Increases item drop chance by {0}%",
            Category = "Utility",
            MaxPoints = 10,
            EffectType = "ItemFind",
            EffectValuePerPoint = 3f, // 3% per point, max 30%
            TierRequired = 2
        },
        new SkillDefinition
        {
            Id = "experience_gain",
            Name = "Fast Learner",
            Description = "Increases experience gained by {0}%",
            Category = "Utility",
            MaxPoints = 5,
            EffectType = "ExperienceGain",
            EffectValuePerPoint = 5f, // 5% per point, max 25%
            TierRequired = 1
        },
    };
}
