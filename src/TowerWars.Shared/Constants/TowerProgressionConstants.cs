namespace TowerWars.Shared.Constants;

public static class TowerProgressionConstants
{
    public const int MaxLevel = 15;
    public const int SkillPointsPerLevel = 1;
    public const int StartingInventorySlots = 50;

    /// <summary>
    /// Cumulative XP required to reach each level.
    /// Index 0 = Level 1 (0 XP), Index 14 = Level 15 (75000 XP total)
    /// </summary>
    public static readonly long[] XpPerLevel =
    [
        0,      // Level 1
        100,    // Level 2
        250,    // Level 3
        500,    // Level 4
        1000,   // Level 5
        2000,   // Level 6
        3500,   // Level 7
        5500,   // Level 8
        8000,   // Level 9
        12000,  // Level 10
        17500,  // Level 11
        25000,  // Level 12
        35000,  // Level 13
        50000,  // Level 14
        75000   // Level 15
    ];

    /// <summary>
    /// XP awarded for different game events
    /// </summary>
    public static class XpSources
    {
        public const int UnitKill = 1;
        public const int WaveClear = 10;
        public const int MatchComplete = 50;
        public const int Victory = 100;
        public const int PerfectWave = 25;     // No units leaked
        public const int BossKill = 10;
    }

    /// <summary>
    /// Get level from total XP
    /// </summary>
    public static int GetLevelFromXp(long totalXp)
    {
        for (int i = XpPerLevel.Length - 1; i >= 0; i--)
        {
            if (totalXp >= XpPerLevel[i])
                return i + 1;
        }
        return 1;
    }

    /// <summary>
    /// Get total skill points available at a given level
    /// </summary>
    public static int GetTotalSkillPointsForLevel(int level)
    {
        return (level - 1) * SkillPointsPerLevel;
    }

    /// <summary>
    /// Get XP needed for the next level
    /// </summary>
    public static long GetXpForNextLevel(int currentLevel)
    {
        if (currentLevel >= MaxLevel) return 0;
        return XpPerLevel[currentLevel];
    }

    /// <summary>
    /// Get XP progress within current level
    /// </summary>
    public static (long current, long required) GetLevelProgress(long totalXp)
    {
        var level = GetLevelFromXp(totalXp);
        if (level >= MaxLevel) return (0, 0);

        var currentLevelXp = XpPerLevel[level - 1];
        var nextLevelXp = XpPerLevel[level];

        return (totalXp - currentLevelXp, nextLevelXp - currentLevelXp);
    }
}

public static class ItemDropConstants
{
    /// <summary>
    /// Rarity drop weights (must sum to 100)
    /// </summary>
    public const int NormalWeight = 70;
    public const int MagicWeight = 25;
    public const int RareWeight = 5;

    /// <summary>
    /// Affix counts by rarity
    /// </summary>
    public static readonly (int min, int max) NormalAffixCount = (0, 1);
    public static readonly (int min, int max) MagicAffixCount = (1, 2);
    public static readonly (int min, int max) RareAffixCount = (3, 4);

    /// <summary>
    /// Drop chances for different sources
    /// </summary>
    public static class DropChances
    {
        public const float WaveCompletionBase = 0.10f;    // 10% base, scales with wave
        public const float WaveCompletionScaling = 0.01f; // +1% per wave
        public const float BossMagicOrBetter = 0.50f;     // 50% magic+ from bosses
        public const float BossRare = 0.10f;              // 10% rare from bosses
        public const float PerfectWave = 1.0f;            // Guaranteed drop for perfect wave
    }

    /// <summary>
    /// End of match rewards
    /// </summary>
    public static class MatchRewards
    {
        public const int CompletionItems = 1;
        public const int VictoryBonusItems = 1;
        public const int Wave10Milestone = 1;
        public const int Wave20Milestone = 1;
        public const int Wave30Milestone = 1;
    }
}
