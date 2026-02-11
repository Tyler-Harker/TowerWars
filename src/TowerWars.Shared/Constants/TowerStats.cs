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

public enum DamageType : byte
{
    Physical = 0,
    Magic = 1,
    Fire = 2,
    Ice = 3,
    Lightning = 4,
    Poison = 5,
    True = 6
}

public static class TowerDefinitions
{
    public static readonly Dictionary<TowerType, TowerStats> Stats = new()
    {
        [TowerType.Basic] = new TowerStats(
            Cost: 100,
            Damage: 10,
            Range: 3.0f,
            AttackSpeed: 1.0f,
            SellValue: 70,
            DamageType: DamageType.Physical,
            ProjectileSpeed: 10f
        ),

        [TowerType.Archer] = new TowerStats(
            Cost: 150,
            Damage: 15,
            Range: 4.5f,
            AttackSpeed: 1.5f,
            SellValue: 105,
            DamageType: DamageType.Physical,
            ProjectileSpeed: 15f
        ),

        [TowerType.Cannon] = new TowerStats(
            Cost: 250,
            Damage: 40,
            Range: 3.5f,
            AttackSpeed: 0.5f,
            SellValue: 175,
            DamageType: DamageType.Physical,
            ProjectileSpeed: 8f,
            SplashRadius: 1.5f
        ),

        [TowerType.Magic] = new TowerStats(
            Cost: 200,
            Damage: 20,
            Range: 4.0f,
            AttackSpeed: 1.0f,
            SellValue: 140,
            DamageType: DamageType.Magic,
            ProjectileSpeed: 12f
        ),

        [TowerType.Ice] = new TowerStats(
            Cost: 175,
            Damage: 8,
            Range: 3.5f,
            AttackSpeed: 0.8f,
            SellValue: 122,
            DamageType: DamageType.Ice,
            ProjectileSpeed: 10f,
            SlowAmount: 0.3f,
            SlowDuration: 2.0f
        ),

        [TowerType.Fire] = new TowerStats(
            Cost: 225,
            Damage: 25,
            Range: 3.0f,
            AttackSpeed: 0.7f,
            SellValue: 157,
            DamageType: DamageType.Fire,
            ProjectileSpeed: 8f,
            SplashRadius: 1.0f
        ),

        [TowerType.Lightning] = new TowerStats(
            Cost: 300,
            Damage: 35,
            Range: 5.0f,
            AttackSpeed: 0.6f,
            SellValue: 210,
            DamageType: DamageType.Lightning,
            ProjectileSpeed: 0 // Instant hit
        ),

        [TowerType.Poison] = new TowerStats(
            Cost: 200,
            Damage: 5,
            Range: 3.5f,
            AttackSpeed: 1.0f,
            SellValue: 140,
            DamageType: DamageType.Poison,
            ProjectileSpeed: 8f
            // DoT handled separately
        ),

        [TowerType.Support] = new TowerStats(
            Cost: 350,
            Damage: 0,
            Range: 4.0f,
            AttackSpeed: 0,
            SellValue: 245,
            DamageType: DamageType.Physical
            // Buffs nearby towers
        ),

        [TowerType.Ultimate] = new TowerStats(
            Cost: 1000,
            Damage: 100,
            Range: 6.0f,
            AttackSpeed: 0.3f,
            SellValue: 700,
            DamageType: DamageType.True,
            ProjectileSpeed: 20f,
            SplashRadius: 2.0f
        )
    };

    public static TowerStats GetStats(TowerType type) =>
        Stats.TryGetValue(type, out var stats) ? stats : Stats[TowerType.Basic];
}
