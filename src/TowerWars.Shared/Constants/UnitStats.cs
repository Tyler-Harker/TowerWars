namespace TowerWars.Shared.Constants;

public readonly record struct UnitStats(
    int Health,
    float Speed,
    int GoldReward,
    int ScoreValue,
    int LivesCost,
    float Armor = 0,
    float MagicResist = 0,
    bool CanFly = false,
    bool IsInvisible = false,
    int SplitCount = 0,
    UnitType? SplitInto = null
);

public static class UnitDefinitions
{
    public static readonly Dictionary<UnitType, UnitStats> Stats = new()
    {
        [UnitType.Basic] = new UnitStats(
            Health: 100,
            Speed: 2.0f,
            GoldReward: 5,
            ScoreValue: 10,
            LivesCost: 1
        ),

        [UnitType.Fast] = new UnitStats(
            Health: 60,
            Speed: 4.0f,
            GoldReward: 8,
            ScoreValue: 15,
            LivesCost: 1
        ),

        [UnitType.Tank] = new UnitStats(
            Health: 500,
            Speed: 1.0f,
            GoldReward: 20,
            ScoreValue: 30,
            LivesCost: 2,
            Armor: 5
        ),

        [UnitType.Flying] = new UnitStats(
            Health: 80,
            Speed: 3.0f,
            GoldReward: 10,
            ScoreValue: 20,
            LivesCost: 1,
            CanFly: true
        ),

        [UnitType.Swarm] = new UnitStats(
            Health: 30,
            Speed: 2.5f,
            GoldReward: 2,
            ScoreValue: 5,
            LivesCost: 1
        ),

        [UnitType.Healer] = new UnitStats(
            Health: 150,
            Speed: 1.5f,
            GoldReward: 15,
            ScoreValue: 25,
            LivesCost: 1
            // Heals nearby units
        ),

        [UnitType.Shield] = new UnitStats(
            Health: 200,
            Speed: 1.8f,
            GoldReward: 12,
            ScoreValue: 20,
            LivesCost: 1,
            Armor: 3,
            MagicResist: 3
            // Shields nearby units
        ),

        [UnitType.Boss] = new UnitStats(
            Health: 2000,
            Speed: 0.8f,
            GoldReward: 100,
            ScoreValue: 200,
            LivesCost: 10,
            Armor: 10,
            MagicResist: 10
        ),

        [UnitType.Invisible] = new UnitStats(
            Health: 70,
            Speed: 2.5f,
            GoldReward: 12,
            ScoreValue: 20,
            LivesCost: 1,
            IsInvisible: true
        ),

        [UnitType.Splitting] = new UnitStats(
            Health: 200,
            Speed: 1.5f,
            GoldReward: 8,
            ScoreValue: 15,
            LivesCost: 2,
            SplitCount: 2,
            SplitInto: UnitType.Fast
        )
    };

    public static UnitStats GetStats(UnitType type) =>
        Stats.TryGetValue(type, out var stats) ? stats : Stats[UnitType.Basic];

    public static UnitStats ScaleForWave(UnitType type, int waveNumber)
    {
        var baseStats = GetStats(type);
        var healthMultiplier = 1.0f + (waveNumber * 0.1f);
        var speedMultiplier = 1.0f + (waveNumber * 0.02f);

        return baseStats with
        {
            Health = (int)(baseStats.Health * healthMultiplier),
            Speed = Math.Min(baseStats.Speed * speedMultiplier, baseStats.Speed * 2)
        };
    }
}
