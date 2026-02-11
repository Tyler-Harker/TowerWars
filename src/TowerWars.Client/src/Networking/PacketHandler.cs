using Godot;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.Networking;

public partial class PacketHandler : Node
{
    [Signal]
    public delegate void MatchStartedEventHandler(string matchId);

    [Signal]
    public delegate void MatchEndedEventHandler(string result);

    [Signal]
    public delegate void WaveStartedEventHandler(int waveNumber);

    [Signal]
    public delegate void WaveEndedEventHandler(int waveNumber, bool success, int bonusGold);

    [Signal]
    public delegate void EntitySpawnedEventHandler(uint entityId, int entityType, float x, float y);

    [Signal]
    public delegate void EntityDestroyedEventHandler(uint entityId);

    [Signal]
    public delegate void EntityUpdatedEventHandler(uint entityId, float x, float y, int health);

    [Signal]
    public delegate void PlayerStateUpdatedEventHandler(uint playerId, int gold, int lives, int score);

    [Signal]
    public delegate void ChatReceivedEventHandler(string senderName, string message);

    [Signal]
    public delegate void ErrorReceivedEventHandler(int code, string message);

    private NetworkManager? _network;

    public override void _Ready()
    {
        _network = GetNode<NetworkManager>("/root/GameManager/NetworkManager");
        _network.PacketReceived += OnPacketReceived;
    }

    private void OnPacketReceived(int packetTypeInt, byte[] payload)
    {
        var packetType = (PacketType)packetTypeInt;
        var payloadMemory = new ReadOnlyMemory<byte>(payload);

        switch (packetType)
        {
            case PacketType.MatchStart:
                HandleMatchStart(PacketSerializer.Deserialize<MatchStartPacket>(payloadMemory));
                break;

            case PacketType.MatchEnd:
                HandleMatchEnd(PacketSerializer.Deserialize<MatchEndPacket>(payloadMemory));
                break;

            case PacketType.WaveStart:
                HandleWaveStart(PacketSerializer.Deserialize<WaveStartPacket>(payloadMemory));
                break;

            case PacketType.WaveEnd:
                HandleWaveEnd(PacketSerializer.Deserialize<WaveEndPacket>(payloadMemory));
                break;

            case PacketType.EntitySpawn:
                HandleEntitySpawn(PacketSerializer.Deserialize<EntitySpawnPacket>(payloadMemory));
                break;

            case PacketType.EntityDestroy:
                HandleEntityDestroy(PacketSerializer.Deserialize<EntityDestroyPacket>(payloadMemory));
                break;

            case PacketType.EntityUpdate:
                HandleEntityUpdate(PacketSerializer.Deserialize<EntityUpdatePacket>(payloadMemory));
                break;

            case PacketType.StateSnapshot:
                HandleStateSnapshot(PacketSerializer.Deserialize<StateSnapshotPacket>(payloadMemory));
                break;

            case PacketType.ChatBroadcast:
                HandleChatBroadcast(PacketSerializer.Deserialize<ChatBroadcastPacket>(payloadMemory));
                break;

            case PacketType.Error:
                HandleError(PacketSerializer.Deserialize<ErrorPacket>(payloadMemory));
                break;
        }
    }

    private void HandleMatchStart(MatchStartPacket packet)
    {
        GD.Print($"Match started: {packet.MatchId}, Mode: {packet.Mode}");
        EmitSignal(SignalName.MatchStarted, packet.MatchId.ToString());
    }

    private void HandleMatchEnd(MatchEndPacket packet)
    {
        GD.Print($"Match ended: {packet.Result}");
        EmitSignal(SignalName.MatchEnded, packet.Result.ToString());
    }

    private void HandleWaveStart(WaveStartPacket packet)
    {
        GD.Print($"Wave {packet.WaveNumber} started");
        EmitSignal(SignalName.WaveStarted, (int)packet.WaveNumber);
    }

    private void HandleWaveEnd(WaveEndPacket packet)
    {
        GD.Print($"Wave {packet.WaveNumber} ended, success: {packet.Success}");
        EmitSignal(SignalName.WaveEnded, (int)packet.WaveNumber, packet.Success, packet.BonusGold);
    }

    private void HandleEntitySpawn(EntitySpawnPacket packet)
    {
        EmitSignal(SignalName.EntitySpawned,
            packet.Entity.EntityId,
            (int)packet.Entity.Type,
            packet.Entity.X,
            packet.Entity.Y);
    }

    private void HandleEntityDestroy(EntityDestroyPacket packet)
    {
        EmitSignal(SignalName.EntityDestroyed, packet.EntityId);
    }

    private void HandleEntityUpdate(EntityUpdatePacket packet)
    {
        foreach (var delta in packet.Deltas)
        {
            if (delta.Flags.HasFlag(DeltaFlags.Position) || delta.Flags.HasFlag(DeltaFlags.Health))
            {
                EmitSignal(SignalName.EntityUpdated,
                    delta.EntityId,
                    delta.X ?? 0,
                    delta.Y ?? 0,
                    delta.Health ?? 0);
            }
        }
    }

    private void HandleStateSnapshot(StateSnapshotPacket packet)
    {
        foreach (var player in packet.Players)
        {
            EmitSignal(SignalName.PlayerStateUpdated,
                player.PlayerId,
                player.Gold,
                player.Lives,
                player.Score);
        }
    }

    private void HandleChatBroadcast(ChatBroadcastPacket packet)
    {
        EmitSignal(SignalName.ChatReceived, packet.SenderName, packet.Message);
    }

    private void HandleError(ErrorPacket packet)
    {
        GD.PrintErr($"Server error: [{packet.Code}] {packet.Message}");
        EmitSignal(SignalName.ErrorReceived, (int)packet.Code, packet.Message);
    }
}
