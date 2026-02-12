using System;
using Godot;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.Networking;

public partial class PacketHandler : Node
{
    // C# events for complex data (Godot signals can't pass complex objects like byte[])
    public event Action<PlayerTowersResponsePacket>? PlayerTowersReceived;
    public event Action<PlayerItemsResponsePacket>? PlayerItemsReceived;
    public event Action<RequestMatchAckPacket>? RequestMatchAckReceived;
    public event Action? ReturnedToLobby;

    // Entity events use C# events because EntitySpawned passes byte[] ExtraData
    // which doesn't marshal correctly through Godot's Variant-based signal system
    public event Action<uint, int, float, float, byte[]>? EntitySpawned;
    public event Action<uint>? EntityDestroyed;
    public event Action<uint, float, float, int>? EntityUpdated;

    [Signal]
    public delegate void MatchStartedEventHandler(string matchId);

    [Signal]
    public delegate void MatchEndedEventHandler(string result);

    [Signal]
    public delegate void WaveStartedEventHandler(int waveNumber);

    [Signal]
    public delegate void WaveEndedEventHandler(int waveNumber, bool success, int bonusGold);

    [Signal]
    public delegate void PlayerStateUpdatedEventHandler(uint playerId, int gold, int lives, int score);

    [Signal]
    public delegate void ChatReceivedEventHandler(string senderName, string message);

    [Signal]
    public delegate void ErrorReceivedEventHandler(int code, string message);

    [Signal]
    public delegate void ItemDroppedEventHandler(uint dropId, float x, float y, int itemType, int rarity, int itemLevel, string name, uint ownerId);

    [Signal]
    public delegate void ItemCollectedEventHandler(uint dropId, bool success, string? itemId, string? error);

    [Signal]
    public delegate void GamePausedEventHandler(bool isPaused, string? reason);

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

            case PacketType.ItemDrop:
                HandleItemDrop(PacketSerializer.Deserialize<ItemDropPacket>(payloadMemory));
                break;

            case PacketType.ItemCollectAck:
                HandleItemCollectAck(PacketSerializer.Deserialize<ItemCollectAckPacket>(payloadMemory));
                break;

            case PacketType.GamePause:
                HandleGamePause(PacketSerializer.Deserialize<GamePausePacket>(payloadMemory));
                break;

            case PacketType.PlayerTowersResponse:
                var towersResponse = PacketSerializer.Deserialize<PlayerTowersResponsePacket>(payloadMemory);
                GD.Print($"Received {towersResponse.Towers.Length} towers from server");
                PlayerTowersReceived?.Invoke(towersResponse);
                break;

            case PacketType.PlayerItemsResponse:
                var itemsResponse = PacketSerializer.Deserialize<PlayerItemsResponsePacket>(payloadMemory);
                GD.Print($"Received {itemsResponse.Items.Length} items from server");
                PlayerItemsReceived?.Invoke(itemsResponse);
                break;

            case PacketType.RequestMatchAck:
                var matchAck = PacketSerializer.Deserialize<RequestMatchAckPacket>(payloadMemory);
                GD.Print($"Match request ack: success={matchAck.Success}, matchId={matchAck.MatchId}");
                RequestMatchAckReceived?.Invoke(matchAck);
                break;

            case PacketType.ReturnToLobby:
                GD.Print("Returned to lobby");
                ReturnedToLobby?.Invoke();
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
        GD.Print($"Entity spawned: id={packet.Entity.EntityId}, type={packet.Entity.Type}, pos=({packet.Entity.X}, {packet.Entity.Y})");
        EntitySpawned?.Invoke(
            packet.Entity.EntityId,
            (int)packet.Entity.Type,
            packet.Entity.X,
            packet.Entity.Y,
            packet.Entity.ExtraData ?? System.Array.Empty<byte>());
    }

    private void HandleEntityDestroy(EntityDestroyPacket packet)
    {
        EntityDestroyed?.Invoke(packet.EntityId);
    }

    private void HandleEntityUpdate(EntityUpdatePacket packet)
    {
        foreach (var delta in packet.Deltas)
        {
            if (delta.Flags.HasFlag(DeltaFlags.Position) || delta.Flags.HasFlag(DeltaFlags.Health))
            {
                EntityUpdated?.Invoke(
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

    private void HandleItemDrop(ItemDropPacket packet)
    {
        GD.Print($"Item dropped: {packet.Name} ({packet.Rarity}) at ({packet.X}, {packet.Y})");
        EmitSignal(SignalName.ItemDropped,
            packet.DropId,
            packet.X,
            packet.Y,
            (int)packet.ItemType,
            (int)packet.Rarity,
            packet.ItemLevel,
            packet.Name,
            packet.OwnerId);
    }

    private void HandleItemCollectAck(ItemCollectAckPacket packet)
    {
        GD.Print($"Item collect ACK: dropId={packet.DropId}, success={packet.Success}");
        EmitSignal(SignalName.ItemCollected,
            packet.DropId,
            packet.Success,
            packet.ItemId?.ToString() ?? string.Empty,
            packet.ErrorMessage ?? string.Empty);
    }

    private void HandleGamePause(GamePausePacket packet)
    {
        GD.Print($"Game {(packet.IsPaused ? "paused" : "resumed")}: {packet.Reason ?? "no reason"}");
        EmitSignal(SignalName.GamePaused, packet.IsPaused, packet.Reason ?? string.Empty);
    }
}
