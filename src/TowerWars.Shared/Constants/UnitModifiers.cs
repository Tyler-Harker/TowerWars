namespace TowerWars.Shared.Constants;

/// <summary>
/// Rarity levels for enemy units
/// </summary>
public enum UnitRarity : byte
{
    Normal = 0,   // No modifiers
    Magic = 1,    // 1-2 modifiers
    Rare = 2      // 2-4 modifiers
}

/// <summary>
/// Modifiers that can be applied to magic/rare units
/// </summary>
[Flags]
public enum UnitModifier : uint
{
    None = 0,

    // Resistances (reduce damage taken)
    PhysicalResistance = 1 << 0,    // 30% physical damage reduction
    FireResistance = 1 << 1,        // 30% fire damage reduction
    ColdResistance = 1 << 2,        // 30% cold damage reduction
    LightningResistance = 1 << 3,   // 30% lightning damage reduction
    PoisonResistance = 1 << 4,      // 30% poison damage reduction

    // Stat modifiers
    Swift = 1 << 5,                 // +40% movement speed
    Hasted = 1 << 6,                // +25% movement speed
    Tough = 1 << 7,                 // +50% health
    Armored = 1 << 8,               // +25% health, +15% all resistance
    Regenerating = 1 << 9,          // Regenerates 2% max health per second

    // Special abilities
    Shielded = 1 << 10,             // Blocks first hit
    Vampiric = 1 << 11,             // Heals nearby allies on death
    Explosive = 1 << 12,            // Damages towers on death
    Splitting = 1 << 13,            // Spawns 2 smaller units on death
}

/// <summary>
/// Constants for unit modifier effects
/// </summary>
public static class UnitModifierConstants
{
    // Rarity roll chances (out of 100)
    public const int MagicChance = 15;      // 15% chance for magic
    public const int RareChance = 5;        // 5% chance for rare

    // Modifier counts by rarity
    public const int MagicModifierMin = 1;
    public const int MagicModifierMax = 2;
    public const int RareModifierMin = 2;
    public const int RareModifierMax = 4;

    // Resistance values (percentage damage reduction)
    public const float ElementalResistanceAmount = 0.30f;  // 30%
    public const float ArmoredResistanceAmount = 0.15f;    // 15%

    // Stat modifier values
    public const float SwiftSpeedBonus = 0.40f;            // +40%
    public const float HasteSpeedBonus = 0.25f;            // +25%
    public const float ToughHealthBonus = 0.50f;           // +50%
    public const float ArmoredHealthBonus = 0.25f;         // +25%
    public const float RegenerationPerSecond = 0.02f;      // 2% max health

    // Reward multipliers
    public const float MagicGoldMultiplier = 1.5f;
    public const float RareGoldMultiplier = 2.5f;
    public const float MagicXpMultiplier = 2.0f;
    public const float RareXpMultiplier = 3.0f;

    // Drop chance multipliers
    public const float MagicDropChanceMultiplier = 2.0f;
    public const float RareDropChanceMultiplier = 5.0f;

    /// <summary>
    /// All available modifiers for rolling
    /// </summary>
    public static readonly UnitModifier[] AllModifiers =
    [
        UnitModifier.PhysicalResistance,
        UnitModifier.FireResistance,
        UnitModifier.ColdResistance,
        UnitModifier.LightningResistance,
        UnitModifier.PoisonResistance,
        UnitModifier.Swift,
        UnitModifier.Hasted,
        UnitModifier.Tough,
        UnitModifier.Armored,
        UnitModifier.Regenerating,
        UnitModifier.Shielded,
        UnitModifier.Vampiric,
        UnitModifier.Explosive,
        UnitModifier.Splitting,
    ];

    /// <summary>
    /// Get display name for a modifier
    /// </summary>
    public static string GetModifierName(UnitModifier modifier) => modifier switch
    {
        UnitModifier.PhysicalResistance => "Physical Resistant",
        UnitModifier.FireResistance => "Fire Resistant",
        UnitModifier.ColdResistance => "Cold Resistant",
        UnitModifier.LightningResistance => "Lightning Resistant",
        UnitModifier.PoisonResistance => "Poison Resistant",
        UnitModifier.Swift => "Swift",
        UnitModifier.Hasted => "Hasted",
        UnitModifier.Tough => "Tough",
        UnitModifier.Armored => "Armored",
        UnitModifier.Regenerating => "Regenerating",
        UnitModifier.Shielded => "Shielded",
        UnitModifier.Vampiric => "Vampiric",
        UnitModifier.Explosive => "Explosive",
        UnitModifier.Splitting => "Splitting",
        _ => "Unknown"
    };

    /// <summary>
    /// Get color for unit rarity
    /// </summary>
    public static (byte R, byte G, byte B) GetRarityColor(UnitRarity rarity) => rarity switch
    {
        UnitRarity.Normal => (255, 255, 255),    // White
        UnitRarity.Magic => (100, 150, 255),     // Blue
        UnitRarity.Rare => (255, 255, 100),      // Yellow
        _ => (255, 255, 255)
    };
}
