using Microsoft.Extensions.Logging;
using TowerWars.Shared.Protocol;
using TowerWars.ZoneServer.Game;

namespace TowerWars.ZoneServer.Networking;

public class PacketRouter
{
    private readonly ILogger<PacketRouter> _logger;
    private readonly GameSession _gameSession;
    private readonly PlayerManager _playerManager;

    public PacketRouter(
        ILogger<PacketRouter> logger,
        GameSession gameSession,
        PlayerManager playerManager)
    {
        _logger = logger;
        _gameSession = gameSession;
        _playerManager = playerManager;
    }

    public void Route(uint peerId, PacketType type, ReadOnlyMemory<byte> payload)
    {
        switch (type)
        {
            case PacketType.Connect:
                HandleConnect(peerId, PacketSerializer.Deserialize<ConnectPacket>(payload));
                break;

            case PacketType.Ping:
                HandlePing(peerId, PacketSerializer.Deserialize<PingPacket>(payload));
                break;

            case PacketType.PlayerInput:
                HandlePlayerInput(peerId, PacketSerializer.Deserialize<PlayerInputPacket>(payload));
                break;

            case PacketType.TowerBuild:
                HandleTowerBuild(peerId, PacketSerializer.Deserialize<TowerBuildPacket>(payload));
                break;

            case PacketType.TowerUpgrade:
                HandleTowerUpgrade(peerId, PacketSerializer.Deserialize<TowerUpgradePacket>(payload));
                break;

            case PacketType.TowerSell:
                HandleTowerSell(peerId, PacketSerializer.Deserialize<TowerSellPacket>(payload));
                break;

            case PacketType.AbilityUse:
                HandleAbilityUse(peerId, PacketSerializer.Deserialize<AbilityUsePacket>(payload));
                break;

            case PacketType.ReadyState:
                HandleReadyState(peerId, PacketSerializer.Deserialize<ReadyStatePacket>(payload));
                break;

            case PacketType.ChatMessage:
                HandleChatMessage(peerId, PacketSerializer.Deserialize<ChatMessagePacket>(payload));
                break;

            default:
                _logger.LogWarning("Unhandled packet type {Type} from peer {PeerId}", type, peerId);
                break;
        }
    }

    private void HandleConnect(uint peerId, ConnectPacket packet)
    {
        if (packet.ProtocolVersion != PacketSerializer.ProtocolVersion)
        {
            _logger.LogWarning("Protocol version mismatch from peer {PeerId}: expected {Expected}, got {Actual}",
                peerId, PacketSerializer.ProtocolVersion, packet.ProtocolVersion);
            return;
        }

        _playerManager.HandleConnectionRequest(peerId, packet.ConnectionToken);
    }

    private void HandlePing(uint peerId, PingPacket packet)
    {
        _playerManager.HandlePing(peerId, packet.ClientTime);
    }

    private void HandlePlayerInput(uint peerId, PlayerInputPacket packet)
    {
        _gameSession.ProcessInput(peerId, packet);
    }

    private void HandleTowerBuild(uint peerId, TowerBuildPacket packet)
    {
        _gameSession.ProcessTowerBuild(peerId, packet);
    }

    private void HandleTowerUpgrade(uint peerId, TowerUpgradePacket packet)
    {
        _gameSession.ProcessTowerUpgrade(peerId, packet);
    }

    private void HandleTowerSell(uint peerId, TowerSellPacket packet)
    {
        _gameSession.ProcessTowerSell(peerId, packet);
    }

    private void HandleAbilityUse(uint peerId, AbilityUsePacket packet)
    {
        _gameSession.ProcessAbilityUse(peerId, packet);
    }

    private void HandleReadyState(uint peerId, ReadyStatePacket packet)
    {
        _playerManager.SetReady(peerId, packet.IsReady);
    }

    private void HandleChatMessage(uint peerId, ChatMessagePacket packet)
    {
        _playerManager.BroadcastChat(peerId, packet);
    }
}
