using MessagePack;
using TowerWars.Shared.Constants;

namespace TowerWars.Shared.Protocol;

[MessagePackObject]
public sealed class EntityState
{
    [Key(0)]
    public required uint EntityId { get; init; }

    [Key(1)]
    public required EntityType Type { get; init; }

    [Key(2)]
    public required float X { get; init; }

    [Key(3)]
    public required float Y { get; init; }

    [Key(4)]
    public required float Rotation { get; init; }

    [Key(5)]
    public required int Health { get; init; }

    [Key(6)]
    public required int MaxHealth { get; init; }

    [Key(7)]
    public uint? OwnerId { get; init; }

    [Key(8)]
    public byte[]? ExtraData { get; init; }
}

[MessagePackObject]
public sealed class EntityDelta
{
    [Key(0)]
    public required uint EntityId { get; init; }

    [Key(1)]
    public DeltaFlags Flags { get; init; }

    [Key(2)]
    public float? X { get; init; }

    [Key(3)]
    public float? Y { get; init; }

    [Key(4)]
    public float? Rotation { get; init; }

    [Key(5)]
    public int? Health { get; init; }

    [Key(6)]
    public byte[]? ExtraData { get; init; }
}

[Flags]
public enum DeltaFlags : byte
{
    None = 0,
    Position = 1 << 0,
    Rotation = 1 << 1,
    Health = 1 << 2,
    ExtraData = 1 << 3
}

public enum EntityType : byte
{
    Unknown = 0,
    Tower = 1,
    Unit = 2,
    Projectile = 3,
    Effect = 4,
    Resource = 5
}

[MessagePackObject]
public sealed class PlayerState
{
    [Key(0)]
    public required uint PlayerId { get; init; }

    [Key(1)]
    public required string Name { get; init; }

    [Key(2)]
    public required int Gold { get; init; }

    [Key(3)]
    public required int Lives { get; init; }

    [Key(4)]
    public required int Score { get; init; }

    [Key(5)]
    public required byte TeamId { get; init; }

    [Key(6)]
    public required bool IsReady { get; init; }

    [Key(7)]
    public required bool IsConnected { get; init; }
}

[MessagePackObject]
public sealed class PlayerInfo
{
    [Key(0)]
    public required uint PlayerId { get; init; }

    [Key(1)]
    public required Guid UserId { get; init; }

    [Key(2)]
    public required string Name { get; init; }

    [Key(3)]
    public required byte TeamId { get; init; }

    [Key(4)]
    public required int EloRating { get; init; }
}

[MessagePackObject]
public sealed class MapInfo
{
    [Key(0)]
    public required string MapId { get; init; }

    [Key(1)]
    public required int Width { get; init; }

    [Key(2)]
    public required int Height { get; init; }

    [Key(3)]
    public required PathPoint[][] Paths { get; init; }

    [Key(4)]
    public required BuildableZone[] BuildableZones { get; init; }
}

[MessagePackObject]
public sealed class PathPoint
{
    [Key(0)]
    public required int X { get; init; }

    [Key(1)]
    public required int Y { get; init; }
}

[MessagePackObject]
public sealed class BuildableZone
{
    [Key(0)]
    public required int X { get; init; }

    [Key(1)]
    public required int Y { get; init; }

    [Key(2)]
    public required int Width { get; init; }

    [Key(3)]
    public required int Height { get; init; }

    [Key(4)]
    public required byte TeamId { get; init; }
}

[MessagePackObject]
public sealed class WaveInfo
{
    [Key(0)]
    public required UnitSpawnInfo[] Units { get; init; }

    [Key(1)]
    public required float SpawnInterval { get; init; }

    [Key(2)]
    public required int TotalUnits { get; init; }
}

[MessagePackObject]
public sealed class UnitSpawnInfo
{
    [Key(0)]
    public required UnitType Type { get; init; }

    [Key(1)]
    public required int Count { get; init; }

    [Key(2)]
    public required byte PathIndex { get; init; }
}

public enum MatchResult : byte
{
    Unknown = 0,
    Victory = 1,
    Defeat = 2,
    Draw = 3,
    Abandoned = 4
}

[MessagePackObject]
public sealed class MatchStats
{
    [Key(0)]
    public required int TotalWaves { get; init; }

    [Key(1)]
    public required int UnitsKilled { get; init; }

    [Key(2)]
    public required int TowersBuilt { get; init; }

    [Key(3)]
    public required int GoldEarned { get; init; }

    [Key(4)]
    public required float MatchDuration { get; init; }

    [Key(5)]
    public required PlayerMatchStats[] PlayerStats { get; init; }
}

[MessagePackObject]
public sealed class PlayerMatchStats
{
    [Key(0)]
    public required uint PlayerId { get; init; }

    [Key(1)]
    public required int UnitsKilled { get; init; }

    [Key(2)]
    public required int TowersBuilt { get; init; }

    [Key(3)]
    public required int GoldEarned { get; init; }

    [Key(4)]
    public required int DamageDealt { get; init; }

    [Key(5)]
    public required int LivesLost { get; init; }
}

public enum GameMode : byte
{
    Solo = 0,
    Coop = 1,
    PvP = 2
}
