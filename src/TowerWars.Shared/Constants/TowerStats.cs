namespace TowerWars.Shared.Constants;

public readonly record struct TowerStats(
    int Cost,
    int Damage,
    float Range,
    float AttackSpeed,
    int SellValue,
    DamageType DamageType,
    float ProjectileSpeed = 0,
    float SplashRadius = 0,
    float SlowAmount = 0,
    float SlowDuration = 0
);

// DamageType enum is defined in EquipmentTypes.cs

public static class TowerDefinitions
{
    public static readonly Dictionary<TowerType, TowerStats> Stats = new()
    {
        [TowerType.Basic] = new TowerStats(
            Cost: 1,
            Damage: 10,
            Range: 3.0f,
            AttackSpeed: 1.0f,
            SellValue: 1,
            DamageType: DamageType.Physical,
            ProjectileSpeed: 10f
        ),

        [TowerType.Archer] = new TowerStats(
            Cost: 1,
            Damage: 15,
            Range: 4.5f,
            AttackSpeed: 1.5f,
            SellValue: 1,
            DamageType: DamageType.Physical,
            ProjectileSpeed: 15f
        ),

        [TowerType.Cannon] = new TowerStats(
            Cost: 1,
            Damage: 40,
            Range: 3.5f,
            AttackSpeed: 0.5f,
            SellValue: 1,
            DamageType: DamageType.Physical,
            ProjectileSpeed: 8f,
            SplashRadius: 1.5f
        ),

        [TowerType.Magic] = new TowerStats(
            Cost: 1,
            Damage: 20,
            Range: 4.0f,
            AttackSpeed: 1.0f,
            SellValue: 1,
            DamageType: DamageType.Holy,
            ProjectileSpeed: 12f
        ),

        [TowerType.Ice] = new TowerStats(
            Cost: 1,
            Damage: 8,
            Range: 3.5f,
            AttackSpeed: 0.8f,
            SellValue: 1,
            DamageType: DamageType.Cold,
            ProjectileSpeed: 10f,
            SlowAmount: 0.3f,
            SlowDuration: 2.0f
        ),

        [TowerType.Fire] = new TowerStats(
            Cost: 1,
            Damage: 25,
            Range: 3.0f,
            AttackSpeed: 0.7f,
            SellValue: 1,
            DamageType: DamageType.Fire,
            ProjectileSpeed: 8f,
            SplashRadius: 1.0f
        ),

        [TowerType.Lightning] = new TowerStats(
            Cost: 1,
            Damage: 35,
            Range: 5.0f,
            AttackSpeed: 0.6f,
            SellValue: 1,
            DamageType: DamageType.Lightning,
            ProjectileSpeed: 0 // Instant hit
        ),

        [TowerType.Poison] = new TowerStats(
            Cost: 1,
            Damage: 5,
            Range: 3.5f,
            AttackSpeed: 1.0f,
            SellValue: 1,
            DamageType: DamageType.Chaos,  // Chaos represents poison/void damage
            ProjectileSpeed: 8f
            // DoT handled separately
        ),

        [TowerType.Support] = new TowerStats(
            Cost: 1,
            Damage: 0,
            Range: 4.0f,
            AttackSpeed: 0,
            SellValue: 1,
            DamageType: DamageType.Physical
            // Buffs nearby towers
        ),

        [TowerType.Ultimate] = new TowerStats(
            Cost: 1,
            Damage: 100,
            Range: 6.0f,
            AttackSpeed: 0.3f,
            SellValue: 1,
            DamageType: DamageType.Holy,  // Holy represents pure/true damage
            ProjectileSpeed: 20f,
            SplashRadius: 2.0f
        )
    };

    public static TowerStats GetStats(TowerType type) =>
        Stats.TryGetValue(type, out var stats) ? stats : Stats[TowerType.Basic];
}
