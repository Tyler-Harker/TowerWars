using TowerWars.Shared.Protocol;

namespace TowerWars.Shared.DTOs;

public interface IGameEvent
{
    string EventType { get; }
    Guid MatchId { get; }
    DateTime Timestamp { get; }
}

public sealed record MatchStartedEvent(
    Guid MatchId,
    GameMode Mode,
    Guid[] PlayerIds,
    string MapId,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "match.started";
}

public sealed record MatchEndedEvent(
    Guid MatchId,
    MatchResult Result,
    Guid? WinnerId,
    int WavesCompleted,
    float DurationSeconds,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "match.ended";
}

public sealed record WaveCompletedEvent(
    Guid MatchId,
    int WaveNumber,
    int UnitsKilled,
    int UnitsLeaked,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "wave.completed";
}

public sealed record PlayerDamagedEvent(
    Guid MatchId,
    Guid PlayerId,
    int Damage,
    int RemainingLives,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "player.damaged";
}

public sealed record PlayerEliminatedEvent(
    Guid MatchId,
    Guid PlayerId,
    int WaveReached,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "player.eliminated";
}

public sealed record TowerBuiltEvent(
    Guid MatchId,
    Guid PlayerId,
    uint TowerId,
    byte TowerType,
    int GridX,
    int GridY,
    int GoldSpent,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "tower.built";
}

public sealed record TowerSoldEvent(
    Guid MatchId,
    Guid PlayerId,
    uint TowerId,
    int GoldReceived,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "tower.sold";
}

public sealed record UnitKilledEvent(
    Guid MatchId,
    Guid PlayerId,
    uint UnitId,
    byte UnitType,
    uint? KillerTowerId,
    int GoldAwarded,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "unit.killed";
}

public sealed record AbilityUsedEvent(
    Guid MatchId,
    Guid PlayerId,
    byte AbilityType,
    float TargetX,
    float TargetY,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "ability.used";
}

public sealed record PlayerConnectedEvent(
    Guid MatchId,
    Guid PlayerId,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "player.connected";
}

public sealed record PlayerDisconnectedEvent(
    Guid MatchId,
    Guid PlayerId,
    string Reason,
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "player.disconnected";
}

public sealed record TowerXpGainedEvent(
    Guid MatchId,
    Guid PlayerId,
    byte TowerType,
    int XpAmount,
    string Source,  // "unit_kill", "wave_clear", "match_complete", "victory", "perfect_wave", "boss_kill"
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "tower.xp_gained";
}

public sealed record ItemDroppedEvent(
    Guid MatchId,
    Guid PlayerId,
    Guid ItemId,
    byte Rarity,
    byte ItemType,
    string Source,  // "wave_completion", "boss_kill", "perfect_wave", "match_reward"
    DateTime Timestamp
) : IGameEvent
{
    public string EventType => "item.dropped";
}
