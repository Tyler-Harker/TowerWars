using Microsoft.Extensions.Logging;
using TowerWars.Shared.Protocol;
using TowerWars.ZoneServer.Game;
using TowerWars.ZoneServer.Services;

namespace TowerWars.ZoneServer.Networking;

public class PacketRouter
{
    private readonly ILogger<PacketRouter> _logger;
    private readonly ConnectionManager _connectionManager;
    private readonly GameSessionManager _sessionManager;
    private readonly IPlayerDataService _playerDataService;
    private readonly Lazy<ENetServer> _serverLazy;
    private ENetServer _server => _serverLazy.Value;

    public PacketRouter(
        ILogger<PacketRouter> logger,
        ConnectionManager connectionManager,
        GameSessionManager sessionManager,
        IPlayerDataService playerDataService,
        Lazy<ENetServer> server)
    {
        _logger = logger;
        _connectionManager = connectionManager;
        _sessionManager = sessionManager;
        _playerDataService = playerDataService;
        _serverLazy = server;
    }

    public void Route(uint peerId, PacketType type, ReadOnlyMemory<byte> payload)
    {
        // Connection-level packets — always handled regardless of state
        switch (type)
        {
            case PacketType.Connect:
                HandleConnect(peerId, PacketSerializer.Deserialize<ConnectPacket>(payload));
                return;
            case PacketType.Ping:
                _connectionManager.HandlePing(peerId, PacketSerializer.Deserialize<PingPacket>(payload).ClientTime);
                return;
        }

        // Everything else requires authentication
        var peer = _connectionManager.GetPeer(peerId);
        if (peer == null || peer.State == PeerState.Unauthenticated)
        {
            _logger.LogWarning("Packet {Type} from unauthenticated peer {PeerId}", type, peerId);
            return;
        }

        // Lobby-level packets — authenticated but not necessarily in a game
        switch (type)
        {
            case PacketType.PlayerDataRequest:
                HandlePlayerDataRequest(peerId, peer);
                return;
            case PacketType.RequestMatch:
                _sessionManager.HandleRequestMatch(peerId,
                    PacketSerializer.Deserialize<RequestMatchPacket>(payload));
                return;
        }

        // Game-level packets — peer must be in a game session
        if (peer.State != PeerState.InGame)
        {
            _logger.LogWarning("Game packet {Type} from non-game peer {PeerId} (state: {State})",
                type, peerId, peer.State);
            return;
        }

        var session = _sessionManager.GetSessionForPeer(peerId);
        if (session == null)
        {
            _logger.LogWarning("No session found for in-game peer {PeerId}", peerId);
            return;
        }

        switch (type)
        {
            case PacketType.PlayerInput:
                session.ProcessInput(peerId, PacketSerializer.Deserialize<PlayerInputPacket>(payload));
                break;
            case PacketType.TowerBuild:
                session.ProcessTowerBuild(peerId, PacketSerializer.Deserialize<TowerBuildPacket>(payload));
                break;
            case PacketType.TowerUpgrade:
                session.ProcessTowerUpgrade(peerId, PacketSerializer.Deserialize<TowerUpgradePacket>(payload));
                break;
            case PacketType.TowerSell:
                session.ProcessTowerSell(peerId, PacketSerializer.Deserialize<TowerSellPacket>(payload));
                break;
            case PacketType.AbilityUse:
                session.ProcessAbilityUse(peerId, PacketSerializer.Deserialize<AbilityUsePacket>(payload));
                break;
            case PacketType.ReadyState:
                session.PlayerManager.SetReady(peerId,
                    PacketSerializer.Deserialize<ReadyStatePacket>(payload).IsReady);
                break;
            case PacketType.ChatMessage:
                session.PlayerManager.BroadcastChat(peerId,
                    PacketSerializer.Deserialize<ChatMessagePacket>(payload));
                break;
            case PacketType.ItemCollect:
                session.ProcessItemCollect(peerId, PacketSerializer.Deserialize<ItemCollectPacket>(payload));
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

        _connectionManager.HandleConnectionRequest(peerId, packet.ConnectionToken);
    }

    private async void HandlePlayerDataRequest(uint peerId, ConnectedPeerInfo peer)
    {
        try
        {
            var (towers, items) = await _playerDataService.GetPlayerDataAsync(peer.UserId);

            _server.Send(peerId, new PlayerTowersResponsePacket
            {
                Success = true,
                Towers = towers
            });

            _server.Send(peerId, new PlayerItemsResponsePacket
            {
                Success = true,
                Items = items
            });

            _logger.LogDebug("Sent player data to peer {PeerId}: {TowerCount} towers, {ItemCount} items",
                peerId, towers.Length, items.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load player data for {UserId}", peer.UserId);

            _server.Send(peerId, new PlayerTowersResponsePacket
            {
                Success = false,
                ErrorMessage = "Failed to load player data",
                Towers = []
            });

            _server.Send(peerId, new PlayerItemsResponsePacket
            {
                Success = false,
                ErrorMessage = "Failed to load player data",
                Items = []
            });
        }
    }
}
