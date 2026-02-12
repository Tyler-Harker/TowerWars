using Microsoft.Extensions.Logging;
using TowerWars.Shared.Constants;
using TowerWars.Shared.Protocol;
using TowerWars.ZoneServer.Networking;
using TowerWars.ZoneServer.Services;

namespace TowerWars.ZoneServer.Game;

public enum PeerState
{
    Unauthenticated,
    Lobby,
    InGame
}

public sealed class ConnectedPeerInfo
{
    public uint PeerId { get; init; }
    public Guid UserId { get; set; }
    public Guid CharacterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PeerState State { get; set; } = PeerState.Unauthenticated;
    public Guid? CurrentSessionId { get; set; }
}

public class ConnectionManager
{
    private readonly ILogger<ConnectionManager> _logger;
    private readonly Lazy<ENetServer> _serverLazy;
    private ENetServer _server => _serverLazy.Value;
    private readonly ITokenValidationService _tokenService;

    private readonly Dictionary<uint, ConnectedPeerInfo> _peers = new();
    private readonly object _lock = new();
    private bool _eventsWired;

    public event Action<ConnectedPeerInfo>? OnPeerAuthenticated;
    public event Action<ConnectedPeerInfo>? OnPeerDisconnected;

    public ConnectionManager(
        ILogger<ConnectionManager> logger,
        Lazy<ENetServer> server,
        ITokenValidationService tokenService)
    {
        _logger = logger;
        _serverLazy = server;
        _tokenService = tokenService;
    }

    public void EnsureEventsWired()
    {
        if (_eventsWired) return;
        _eventsWired = true;
        _server.OnPeerConnected += HandlePeerConnected;
        _server.OnPeerDisconnected += HandlePeerDisconnected;
    }

    public async void HandleConnectionRequest(uint peerId, string connectionToken)
    {
        var validation = await _tokenService.ValidateAsync(connectionToken);
        if (validation == null)
        {
            _logger.LogWarning("Invalid connection token from peer {PeerId}", peerId);
            _server.Send(peerId, new AuthResponsePacket { Success = false, ErrorMessage = "Invalid token" });
            _server.Disconnect(peerId, "Invalid token");
            return;
        }

        lock (_lock)
        {
            if (!_peers.TryGetValue(peerId, out var peer))
                return;

            peer.UserId = validation.Value.UserId;
            peer.CharacterId = validation.Value.CharacterId;
            peer.Name = $"Player-{peerId}";
            peer.State = PeerState.Lobby;

            _server.Send(peerId, new ConnectAckPacket
            {
                PlayerId = peerId,
                ServerTick = 0,
                TickRate = GameConstants.DefaultTickRate
            });

            _server.Send(peerId, new AuthResponsePacket { Success = true });

            _logger.LogInformation("Peer {PeerId} ({UserId}) authenticated, now in Lobby", peerId, validation.Value.UserId);

            OnPeerAuthenticated?.Invoke(peer);
        }
    }

    public void HandlePing(uint peerId, long clientTime)
    {
        var serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _server.Send(peerId, new PongPacket { ClientTime = clientTime, ServerTime = serverTime });
    }

    public ConnectedPeerInfo? GetPeer(uint peerId)
    {
        lock (_lock)
        {
            return _peers.TryGetValue(peerId, out var peer) ? peer : null;
        }
    }

    public void SetPeerState(uint peerId, PeerState state, Guid? sessionId = null)
    {
        lock (_lock)
        {
            if (_peers.TryGetValue(peerId, out var peer))
            {
                peer.State = state;
                peer.CurrentSessionId = sessionId;
            }
        }
    }

    private void HandlePeerConnected(uint peerId)
    {
        lock (_lock)
        {
            _peers[peerId] = new ConnectedPeerInfo { PeerId = peerId };
        }
        _logger.LogDebug("Peer {PeerId} connected, awaiting authentication", peerId);
    }

    private void HandlePeerDisconnected(uint peerId, string reason)
    {
        ConnectedPeerInfo? peer;
        lock (_lock)
        {
            if (!_peers.TryGetValue(peerId, out peer))
                return;
            _peers.Remove(peerId);
        }

        _logger.LogInformation("Peer {PeerId} disconnected: {Reason}", peerId, reason);
        OnPeerDisconnected?.Invoke(peer);
    }
}
