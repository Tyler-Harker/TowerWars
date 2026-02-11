using MessagePack;
using TowerWars.Shared.Constants;

namespace TowerWars.Shared.Protocol;

public interface IPacket
{
    PacketType Type { get; }
}

[MessagePackObject]
public sealed class ConnectPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.Connect;

    [Key(0)]
    public required string ConnectionToken { get; init; }

    [Key(1)]
    public required uint ProtocolVersion { get; init; }
}

[MessagePackObject]
public sealed class ConnectAckPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.ConnectAck;

    [Key(0)]
    public required uint PlayerId { get; init; }

    [Key(1)]
    public required uint ServerTick { get; init; }

    [Key(2)]
    public required float TickRate { get; init; }
}

[MessagePackObject]
public sealed class DisconnectPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.Disconnect;

    [Key(0)]
    public required string Reason { get; init; }
}

[MessagePackObject]
public sealed class PingPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.Ping;

    [Key(0)]
    public required long ClientTime { get; init; }
}

[MessagePackObject]
public sealed class PongPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.Pong;

    [Key(0)]
    public required long ClientTime { get; init; }

    [Key(1)]
    public required long ServerTime { get; init; }
}

[MessagePackObject]
public sealed class AuthRequestPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.AuthRequest;

    [Key(0)]
    public required string Token { get; init; }

    [Key(1)]
    public required Guid CharacterId { get; init; }
}

[MessagePackObject]
public sealed class AuthResponsePacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.AuthResponse;

    [Key(0)]
    public required bool Success { get; init; }

    [Key(1)]
    public string? ErrorMessage { get; init; }
}

[MessagePackObject]
public sealed class PlayerInputPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.PlayerInput;

    [Key(0)]
    public required uint InputSequence { get; init; }

    [Key(1)]
    public required uint Tick { get; init; }

    [Key(2)]
    public required InputFlags Flags { get; init; }

    [Key(3)]
    public required float MouseX { get; init; }

    [Key(4)]
    public required float MouseY { get; init; }
}

[Flags]
public enum InputFlags : ushort
{
    None = 0,
    MoveUp = 1 << 0,
    MoveDown = 1 << 1,
    MoveLeft = 1 << 2,
    MoveRight = 1 << 3,
    PrimaryAction = 1 << 4,
    SecondaryAction = 1 << 5,
    Ability1 = 1 << 6,
    Ability2 = 1 << 7,
    Ability3 = 1 << 8,
    Ability4 = 1 << 9
}

[MessagePackObject]
public sealed class PlayerInputAckPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.PlayerInputAck;

    [Key(0)]
    public required uint LastProcessedSequence { get; init; }
}

[MessagePackObject]
public sealed class StateSnapshotPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.StateSnapshot;

    [Key(0)]
    public required uint Tick { get; init; }

    [Key(1)]
    public required EntityState[] Entities { get; init; }

    [Key(2)]
    public required PlayerState[] Players { get; init; }
}

[MessagePackObject]
public sealed class EntityUpdatePacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.EntityUpdate;

    [Key(0)]
    public required uint Tick { get; init; }

    [Key(1)]
    public required EntityDelta[] Deltas { get; init; }
}

[MessagePackObject]
public sealed class EntitySpawnPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.EntitySpawn;

    [Key(0)]
    public required uint Tick { get; init; }

    [Key(1)]
    public required EntityState Entity { get; init; }
}

[MessagePackObject]
public sealed class EntityDestroyPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.EntityDestroy;

    [Key(0)]
    public required uint Tick { get; init; }

    [Key(1)]
    public required uint EntityId { get; init; }

    [Key(2)]
    public DestroyReason Reason { get; init; }
}

public enum DestroyReason : byte
{
    Unknown = 0,
    Killed = 1,
    Sold = 2,
    Expired = 3,
    ReachedEnd = 4
}

[MessagePackObject]
public sealed class TowerBuildPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.TowerBuild;

    [Key(0)]
    public required uint RequestId { get; init; }

    [Key(1)]
    public required TowerType TowerType { get; init; }

    [Key(2)]
    public required int GridX { get; init; }

    [Key(3)]
    public required int GridY { get; init; }
}

[MessagePackObject]
public sealed class TowerUpgradePacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.TowerUpgrade;

    [Key(0)]
    public required uint RequestId { get; init; }

    [Key(1)]
    public required uint TowerId { get; init; }

    [Key(2)]
    public required UpgradePath Path { get; init; }
}

public enum UpgradePath : byte
{
    Path1 = 0,
    Path2 = 1,
    Path3 = 2
}

[MessagePackObject]
public sealed class TowerSellPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.TowerSell;

    [Key(0)]
    public required uint RequestId { get; init; }

    [Key(1)]
    public required uint TowerId { get; init; }
}

[MessagePackObject]
public sealed class AbilityUsePacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.AbilityUse;

    [Key(0)]
    public required uint RequestId { get; init; }

    [Key(1)]
    public required AbilityType AbilityType { get; init; }

    [Key(2)]
    public required float TargetX { get; init; }

    [Key(3)]
    public required float TargetY { get; init; }

    [Key(4)]
    public uint? TargetEntityId { get; init; }
}

[MessagePackObject]
public sealed class MatchStartPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.MatchStart;

    [Key(0)]
    public required Guid MatchId { get; init; }

    [Key(1)]
    public required GameMode Mode { get; init; }

    [Key(2)]
    public required PlayerInfo[] Players { get; init; }

    [Key(3)]
    public required MapInfo Map { get; init; }
}

[MessagePackObject]
public sealed class MatchEndPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.MatchEnd;

    [Key(0)]
    public required Guid MatchId { get; init; }

    [Key(1)]
    public required MatchResult Result { get; init; }

    [Key(2)]
    public required MatchStats Stats { get; init; }
}

[MessagePackObject]
public sealed class WaveStartPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.WaveStart;

    [Key(0)]
    public required uint WaveNumber { get; init; }

    [Key(1)]
    public required WaveInfo WaveInfo { get; init; }
}

[MessagePackObject]
public sealed class WaveEndPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.WaveEnd;

    [Key(0)]
    public required uint WaveNumber { get; init; }

    [Key(1)]
    public required bool Success { get; init; }

    [Key(2)]
    public required int BonusGold { get; init; }
}

[MessagePackObject]
public sealed class ReadyStatePacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.ReadyState;

    [Key(0)]
    public required bool IsReady { get; init; }
}

[MessagePackObject]
public sealed class ChatMessagePacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.ChatMessage;

    [Key(0)]
    public required ChatChannel Channel { get; init; }

    [Key(1)]
    public required string Message { get; init; }

    [Key(2)]
    public uint? TargetPlayerId { get; init; }
}

[MessagePackObject]
public sealed class ChatBroadcastPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.ChatBroadcast;

    [Key(0)]
    public required ChatChannel Channel { get; init; }

    [Key(1)]
    public required uint SenderId { get; init; }

    [Key(2)]
    public required string SenderName { get; init; }

    [Key(3)]
    public required string Message { get; init; }

    [Key(4)]
    public required long Timestamp { get; init; }
}

public enum ChatChannel : byte
{
    Global = 0,
    Team = 1,
    Party = 2,
    Whisper = 3
}

[MessagePackObject]
public sealed class ErrorPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.Error;

    [Key(0)]
    public required ErrorCode Code { get; init; }

    [Key(1)]
    public required string Message { get; init; }

    [Key(2)]
    public uint? RequestId { get; init; }
}

public enum ErrorCode : ushort
{
    Unknown = 0,
    InvalidToken = 1,
    NotAuthenticated = 2,
    InsufficientGold = 3,
    InvalidPlacement = 4,
    TowerNotFound = 5,
    AbilityOnCooldown = 6,
    MatchNotStarted = 7,
    MatchAlreadyStarted = 8,
    PlayerNotFound = 9,
    RateLimited = 10
}
