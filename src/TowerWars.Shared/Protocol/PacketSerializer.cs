using MessagePack;

namespace TowerWars.Shared.Protocol;

public static class PacketSerializer
{
    public const uint ProtocolVersion = 1;

    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithCompression(MessagePackCompression.Lz4BlockArray);

    public static byte[] Serialize<T>(T packet) where T : IPacket
    {
        var payload = MessagePackSerializer.Serialize(packet, Options);
        var result = new byte[payload.Length + 1];
        result[0] = (byte)packet.Type;
        Buffer.BlockCopy(payload, 0, result, 1, payload.Length);
        return result;
    }

    public static (PacketType Type, ReadOnlyMemory<byte> Payload) Peek(ReadOnlySpan<byte> data)
    {
        if (data.Length < 1)
            throw new ArgumentException("Packet too short", nameof(data));

        return ((PacketType)data[0], data[1..].ToArray());
    }

    public static T Deserialize<T>(ReadOnlyMemory<byte> payload) where T : IPacket
    {
        return MessagePackSerializer.Deserialize<T>(payload, Options);
    }

    public static IPacket Deserialize(PacketType type, ReadOnlyMemory<byte> payload)
    {
        return type switch
        {
            PacketType.Connect => MessagePackSerializer.Deserialize<ConnectPacket>(payload, Options),
            PacketType.ConnectAck => MessagePackSerializer.Deserialize<ConnectAckPacket>(payload, Options),
            PacketType.Disconnect => MessagePackSerializer.Deserialize<DisconnectPacket>(payload, Options),
            PacketType.Ping => MessagePackSerializer.Deserialize<PingPacket>(payload, Options),
            PacketType.Pong => MessagePackSerializer.Deserialize<PongPacket>(payload, Options),
            PacketType.AuthRequest => MessagePackSerializer.Deserialize<AuthRequestPacket>(payload, Options),
            PacketType.AuthResponse => MessagePackSerializer.Deserialize<AuthResponsePacket>(payload, Options),
            PacketType.PlayerInput => MessagePackSerializer.Deserialize<PlayerInputPacket>(payload, Options),
            PacketType.PlayerInputAck => MessagePackSerializer.Deserialize<PlayerInputAckPacket>(payload, Options),
            PacketType.StateSnapshot => MessagePackSerializer.Deserialize<StateSnapshotPacket>(payload, Options),
            PacketType.EntityUpdate => MessagePackSerializer.Deserialize<EntityUpdatePacket>(payload, Options),
            PacketType.EntitySpawn => MessagePackSerializer.Deserialize<EntitySpawnPacket>(payload, Options),
            PacketType.EntityDestroy => MessagePackSerializer.Deserialize<EntityDestroyPacket>(payload, Options),
            PacketType.TowerBuild => MessagePackSerializer.Deserialize<TowerBuildPacket>(payload, Options),
            PacketType.TowerUpgrade => MessagePackSerializer.Deserialize<TowerUpgradePacket>(payload, Options),
            PacketType.TowerSell => MessagePackSerializer.Deserialize<TowerSellPacket>(payload, Options),
            PacketType.AbilityUse => MessagePackSerializer.Deserialize<AbilityUsePacket>(payload, Options),
            PacketType.MatchStart => MessagePackSerializer.Deserialize<MatchStartPacket>(payload, Options),
            PacketType.MatchEnd => MessagePackSerializer.Deserialize<MatchEndPacket>(payload, Options),
            PacketType.WaveStart => MessagePackSerializer.Deserialize<WaveStartPacket>(payload, Options),
            PacketType.WaveEnd => MessagePackSerializer.Deserialize<WaveEndPacket>(payload, Options),
            PacketType.ReadyState => MessagePackSerializer.Deserialize<ReadyStatePacket>(payload, Options),
            PacketType.ChatMessage => MessagePackSerializer.Deserialize<ChatMessagePacket>(payload, Options),
            PacketType.ChatBroadcast => MessagePackSerializer.Deserialize<ChatBroadcastPacket>(payload, Options),
            PacketType.Error => MessagePackSerializer.Deserialize<ErrorPacket>(payload, Options),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown packet type")
        };
    }
}
