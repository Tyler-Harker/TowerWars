namespace TowerWars.Shared.Constants;

/// <summary>
/// Physical weapon types that determine attack style, range, and speed
/// </summary>
public enum WeaponType : byte
{
    Bow = 0,      // Long range, fast attack speed, low damage
    Cannon = 1,   // Medium-long range, slow attack speed, high damage
    Club = 2,     // Short range, medium attack speed, medium damage
    Axe = 3,      // Medium range, medium attack speed, high damage
    Sword = 4     // Short-medium range, fast attack speed, medium damage
}

/// <summary>
/// Elemental damage types that can be applied to any weapon
/// </summary>
public enum DamageType : byte
{
    Physical = 0,   // Neutral, no special effects
    Cold = 1,       // Can freeze/slow enemies
    Lightning = 2,  // Can shock/chain to other enemies
    Fire = 3,       // Can ignite enemies for DoT
    Chaos = 4,      // Bypasses resistances
    Holy = 5        // Extra damage to undead/demons
}

/// <summary>
/// Tower item rarity levels for the game loot system
/// </summary>
public enum TowerItemRarity : byte
{
    Common = 0,     // 0 affixes
    Magic = 1,      // 1-2 affixes
    Rare = 2,       // 3-4 affixes
    Legendary = 3   // 4-6 affixes + special
}

/// <summary>
/// Types of tower items that can be equipped or collected
/// </summary>
public enum TowerItemType : byte
{
    Weapon = 0,
    TowerUpgrade = 1,
    Consumable = 2,
    Material = 3
}

/// <summary>
/// Types of affix effects that can roll on tower items
/// </summary>
public enum AffixEffectType : byte
{
    FlatDamage = 0,
    PercentDamage = 1,
    AttackSpeed = 2,
    Range = 3,
    CritChance = 4,
    CritDamage = 5,
    GoldFind = 6,
    ItemFind = 7,
    SlowOnHit = 8,
    BurnDamage = 9,
    ChainLightning = 10,
    Pierce = 11,
    AreaDamage = 12,
    LifeSteal = 13
}
