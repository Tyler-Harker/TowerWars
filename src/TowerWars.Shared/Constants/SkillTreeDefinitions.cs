using TowerWars.Shared.DTOs;

namespace TowerWars.Shared.Constants;

/// <summary>
/// Static skill tree definitions for all tower types.
/// Universal nodes are available to all towers.
/// Elemental nodes are specific to certain tower types.
/// </summary>
public static class SkillTreeDefinitions
{
    /// <summary>
    /// Universal skill nodes available to all tower types
    /// </summary>
    public static readonly SkillNodeDefinition[] UniversalNodes =
    [
        // Tier 1 - Basic Stats (Level 1+)
        new("damage_1", 1, 0, 0, "Sharpened", "Increases damage by 5%",
            1, 1, TowerBonusType.DamagePercent, 5m, 0m, 1, []),

        new("attack_speed_1", 1, 1, 0, "Quick Hands", "Increases attack speed by 5%",
            1, 1, TowerBonusType.AttackSpeedPercent, 5m, 0m, 1, []),

        new("range_1", 1, 2, 0, "Eagle Eye", "Increases range by 5%",
            1, 1, TowerBonusType.RangePercent, 5m, 0m, 1, []),

        // Tier 2 - Enhanced Stats (Level 3+)
        new("damage_2", 2, 0, 1, "Deadly Force", "Increases damage by 3% per rank",
            1, 3, TowerBonusType.DamagePercent, 3m, 3m, 3, ["damage_1"]),

        new("attack_speed_2", 2, 1, 1, "Rapid Fire", "Increases attack speed by 3% per rank",
            1, 3, TowerBonusType.AttackSpeedPercent, 3m, 3m, 3, ["attack_speed_1"]),

        new("range_2", 2, 2, 1, "Far Sight", "Increases range by 3% per rank",
            1, 3, TowerBonusType.RangePercent, 3m, 3m, 3, ["range_1"]),

        // Tier 2 - Critical Strikes (Level 3+)
        new("crit_chance_1", 2, 3, 1, "Precision", "Grants 3% critical strike chance",
            1, 3, TowerBonusType.CritChance, 3m, 0m, 1, []),

        // Tier 3 - Advanced (Level 5+)
        new("damage_3", 3, 0, 2, "Overwhelming Power", "Increases damage by 10%",
            2, 5, TowerBonusType.DamagePercent, 10m, 0m, 1, ["damage_2"]),

        new("crit_multi_1", 3, 3, 2, "Devastating Blows", "Increases critical damage by 25%",
            2, 5, TowerBonusType.CritMultiplier, 25m, 0m, 1, ["crit_chance_1"]),

        new("crit_chance_2", 3, 4, 2, "Keen Edge", "Grants 2% critical strike chance per rank",
            1, 5, TowerBonusType.CritChance, 2m, 2m, 3, ["crit_chance_1"]),

        // Tier 4 - Mastery (Level 8+)
        new("damage_mastery", 4, 0, 3, "Damage Mastery", "Increases all damage by 15%",
            3, 8, TowerBonusType.DamagePercent, 15m, 0m, 1, ["damage_3"]),

        new("crit_mastery", 4, 3, 3, "Critical Mastery", "Grants 5% crit chance and 50% crit damage",
            3, 8, TowerBonusType.CritChance, 5m, 0m, 1, ["crit_multi_1", "crit_chance_2"]),

        // Tier 5 - Ultimate (Level 12+)
        new("ultimate_damage", 5, 1, 4, "Annihilation", "Increases damage by 25%",
            5, 12, TowerBonusType.DamagePercent, 25m, 0m, 1, ["damage_mastery"]),

        // Utility Branch
        new("gold_find_1", 2, 5, 1, "Prospector", "Increases gold from kills by 5% per rank",
            1, 3, TowerBonusType.GoldFindPercent, 5m, 5m, 3, []),

        new("xp_gain_1", 2, 6, 1, "Scholar", "Increases XP gain by 5% per rank",
            1, 3, TowerBonusType.XpGainPercent, 5m, 5m, 3, []),
    ];

    /// <summary>
    /// Fire-specific nodes for Fire, Cannon towers
    /// </summary>
    public static readonly SkillNodeDefinition[] FireNodes =
    [
        new("fire_damage_1", 2, 7, 1, "Burning Touch", "Adds 5 fire damage",
            1, 3, TowerBonusType.FireDamageFlat, 5m, 0m, 1, []),

        new("fire_damage_2", 3, 7, 2, "Inferno", "Increases fire damage by 10% per rank",
            1, 5, TowerBonusType.FireDamagePercent, 10m, 10m, 3, ["fire_damage_1"]),

        new("splash_1", 3, 8, 2, "Explosive Force", "Increases splash radius by 15%",
            2, 5, TowerBonusType.SplashRadiusPercent, 15m, 0m, 1, ["fire_damage_1"]),

        new("fire_mastery", 4, 7, 3, "Pyromaniac", "Increases fire damage by 25%",
            3, 8, TowerBonusType.FireDamagePercent, 25m, 0m, 1, ["fire_damage_2"]),
    ];

    /// <summary>
    /// Ice-specific nodes for Ice towers
    /// </summary>
    public static readonly SkillNodeDefinition[] IceNodes =
    [
        new("ice_damage_1", 2, 7, 1, "Frost Touch", "Adds 3 ice damage",
            1, 3, TowerBonusType.IceDamageFlat, 3m, 0m, 1, []),

        new("slow_1", 3, 7, 2, "Deep Freeze", "Increases slow amount by 10% per rank",
            1, 5, TowerBonusType.SlowAmountPercent, 10m, 10m, 3, ["ice_damage_1"]),

        new("slow_duration_1", 3, 8, 2, "Lingering Cold", "Increases slow duration by 15%",
            2, 5, TowerBonusType.SlowDurationPercent, 15m, 0m, 1, ["ice_damage_1"]),

        new("ice_mastery", 4, 7, 3, "Frozen Heart", "Increases ice damage by 25% and slow by 15%",
            3, 8, TowerBonusType.IceDamagePercent, 25m, 0m, 1, ["slow_1"]),
    ];

    /// <summary>
    /// Lightning-specific nodes for Lightning towers
    /// </summary>
    public static readonly SkillNodeDefinition[] LightningNodes =
    [
        new("lightning_damage_1", 2, 7, 1, "Static Charge", "Adds 5 lightning damage",
            1, 3, TowerBonusType.LightningDamageFlat, 5m, 0m, 1, []),

        new("lightning_damage_2", 3, 7, 2, "Storm Surge", "Increases lightning damage by 10% per rank",
            1, 5, TowerBonusType.LightningDamagePercent, 10m, 10m, 3, ["lightning_damage_1"]),

        new("chain_range", 3, 8, 2, "Conductivity", "Increases range by 10%",
            2, 5, TowerBonusType.RangePercent, 10m, 0m, 1, ["lightning_damage_1"]),

        new("lightning_mastery", 4, 7, 3, "Thunderlord", "Increases lightning damage by 25%",
            3, 8, TowerBonusType.LightningDamagePercent, 25m, 0m, 1, ["lightning_damage_2"]),
    ];

    /// <summary>
    /// Poison-specific nodes for Poison towers
    /// </summary>
    public static readonly SkillNodeDefinition[] PoisonNodes =
    [
        new("poison_damage_1", 2, 7, 1, "Toxic Touch", "Adds 3 poison damage",
            1, 3, TowerBonusType.PoisonDamageFlat, 3m, 0m, 1, []),

        new("poison_damage_2", 3, 7, 2, "Virulence", "Increases poison damage by 10% per rank",
            1, 5, TowerBonusType.PoisonDamagePercent, 10m, 10m, 3, ["poison_damage_1"]),

        new("poison_mastery", 4, 7, 3, "Plague Bearer", "Increases poison damage by 25%",
            3, 8, TowerBonusType.PoisonDamagePercent, 25m, 0m, 1, ["poison_damage_2"]),
    ];

    /// <summary>
    /// Magic-specific nodes for Magic towers
    /// </summary>
    public static readonly SkillNodeDefinition[] MagicNodes =
    [
        new("magic_damage_1", 2, 7, 1, "Arcane Power", "Increases magic damage by 5%",
            1, 3, TowerBonusType.MagicDamagePercent, 5m, 0m, 1, []),

        new("magic_damage_2", 3, 7, 2, "Arcane Mastery", "Increases magic damage by 8% per rank",
            1, 5, TowerBonusType.MagicDamagePercent, 8m, 8m, 3, ["magic_damage_1"]),

        new("magic_mastery", 4, 7, 3, "Grand Magus", "Increases magic damage by 20%",
            3, 8, TowerBonusType.MagicDamagePercent, 20m, 0m, 1, ["magic_damage_2"]),
    ];

    /// <summary>
    /// Physical-specific nodes for Basic, Archer towers
    /// </summary>
    public static readonly SkillNodeDefinition[] PhysicalNodes =
    [
        new("physical_damage_1", 2, 7, 1, "Armor Piercing", "Increases physical damage by 5%",
            1, 3, TowerBonusType.PhysicalDamagePercent, 5m, 0m, 1, []),

        new("physical_damage_2", 3, 7, 2, "Brutal Force", "Increases physical damage by 8% per rank",
            1, 5, TowerBonusType.PhysicalDamagePercent, 8m, 8m, 3, ["physical_damage_1"]),

        new("physical_mastery", 4, 7, 3, "Weapon Master", "Increases physical damage by 20%",
            3, 8, TowerBonusType.PhysicalDamagePercent, 20m, 0m, 1, ["physical_damage_2"]),
    ];

    /// <summary>
    /// Get all skill nodes for a specific tower type
    /// </summary>
    public static SkillNodeDefinition[] GetNodesForTowerType(TowerType towerType)
    {
        var elementalNodes = towerType switch
        {
            TowerType.Basic => PhysicalNodes,
            TowerType.Archer => PhysicalNodes,
            TowerType.Cannon => FireNodes,
            TowerType.Magic => MagicNodes,
            TowerType.Ice => IceNodes,
            TowerType.Fire => FireNodes,
            TowerType.Lightning => LightningNodes,
            TowerType.Poison => PoisonNodes,
            TowerType.Support => [],
            TowerType.Ultimate => [],
            _ => []
        };

        return [.. UniversalNodes, .. elementalNodes];
    }
}

public sealed record SkillNodeDefinition(
    string NodeId,
    int Tier,
    int PositionX,
    int PositionY,
    string Name,
    string Description,
    int SkillPointsCost,
    int RequiredTowerLevel,
    TowerBonusType BonusType,
    decimal BonusValue,
    decimal BonusValuePerRank,
    int MaxRanks,
    string[] PrerequisiteNodeIds
);
