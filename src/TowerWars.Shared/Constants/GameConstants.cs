namespace TowerWars.Shared.Constants;

public static class GameConstants
{
    // Networking
    public const int DefaultTickRate = 20; // Ticks per second
    public const float TickInterval = 1.0f / DefaultTickRate;
    public const int MaxPlayersPerMatch = 16;
    public const int MaxCoopPlayers = 6;
    public const int MinPvPPlayers = 2;

    // Game Settings
    public const int StartingGold = 10;
    public const int StartingLives = 20;
    public const int GoldPerSecond = 1;
    public const int WaveCompletionBonus = 50;
    public const float TowerSellPercentage = 0.7f;
    public const float TowerCostScalingFactor = 0.2f; // 20% increase per purchase of same tower type

    // Grid
    public const int GridCellSize = 64;
    public const int DefaultMapWidth = 5;     // Solo mode default width
    public const int DefaultMapHeight = 10;   // Solo mode default height

    // Timing
    public const float WavePreparationTime = 30.0f;
    public const float PvPRoundTime = 180.0f;
    public const float MatchmakingTimeout = 60.0f;
    public const float ReconnectionTimeout = 60.0f;

    // Matchmaking
    public const int DefaultEloRating = 1000;
    public const int EloKFactor = 32;
    public const int MaxEloGap = 500;

    // Rate Limiting
    public const int MaxInputsPerSecond = 60;
    public const int MaxChatMessagesPerMinute = 20;
    public const int MaxActionsPerSecond = 10;

    // Cooldowns (seconds)
    public const float GlobalAbilityCooldown = 1.0f;

    // Version
    public const string GameVersion = "0.1.0";
}

public static class TowerCostCalculator
{
    public static int CalculateCost(TowerType type, int purchaseCount)
    {
        var baseCost = TowerDefinitions.GetStats(type).Cost;
        return (int)(baseCost * (1 + purchaseCount * GameConstants.TowerCostScalingFactor));
    }
}
