namespace TowerWars.Shared.Protocol;

public enum PacketType : byte
{
    // Connection
    Connect = 0x01,
    ConnectAck = 0x02,
    Disconnect = 0x03,
    Ping = 0x04,
    Pong = 0x05,

    // Authentication
    AuthRequest = 0x10,
    AuthResponse = 0x11,
    AuthFailed = 0x12,

    // Player Input
    PlayerInput = 0x20,
    PlayerInputAck = 0x21,

    // Game State
    StateSnapshot = 0x30,
    EntityUpdate = 0x31,
    EntitySpawn = 0x32,
    EntityDestroy = 0x33,

    // Game Actions (RPC)
    TowerBuild = 0x40,
    TowerUpgrade = 0x41,
    TowerSell = 0x42,
    AbilityUse = 0x43,
    UnitSpawn = 0x44,
    ItemDrop = 0x45,
    ItemCollect = 0x46,
    ItemCollectAck = 0x47,

    // Match Control
    MatchStart = 0x50,
    MatchEnd = 0x51,
    WaveStart = 0x52,
    WaveEnd = 0x53,
    ReadyState = 0x54,
    GamePause = 0x55,

    // Chat
    ChatMessage = 0x60,
    ChatBroadcast = 0x61,

    // Error
    Error = 0xFF
}
