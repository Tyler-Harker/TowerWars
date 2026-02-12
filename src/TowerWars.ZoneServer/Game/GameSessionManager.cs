using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TowerWars.Shared.Protocol;
using TowerWars.ZoneServer.Networking;
using TowerWars.ZoneServer.Services;

namespace TowerWars.ZoneServer.Game;

public class GameSessionManager
{
    private readonly ILogger<GameSessionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lazy<ENetServer> _serverLazy;
    private ENetServer _server => _serverLazy.Value;
    private readonly ConnectionManager _connectionManager;
    private readonly IEventPublisher _eventPublisher;
    private readonly ITowerBonusService _towerBonusService;

    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();
    private readonly ConcurrentDictionary<uint, Guid> _peerToSession = new();

    public GameSessionManager(
        ILogger<GameSessionManager> logger,
        ILoggerFactory loggerFactory,
        Lazy<ENetServer> server,
        ConnectionManager connectionManager,
        IEventPublisher eventPublisher,
        ITowerBonusService towerBonusService)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _serverLazy = server;
        _connectionManager = connectionManager;
        _eventPublisher = eventPublisher;
        _towerBonusService = towerBonusService;

        _connectionManager.OnPeerDisconnected += HandlePeerDisconnected;
    }

    public GameSession? GetSessionForPeer(uint peerId)
    {
        return _peerToSession.TryGetValue(peerId, out var sessionId)
            && _sessions.TryGetValue(sessionId, out var session)
            ? session : null;
    }

    public void HandleRequestMatch(uint peerId, RequestMatchPacket packet)
    {
        var peer = _connectionManager.GetPeer(peerId);
        if (peer == null || peer.State != PeerState.Lobby)
        {
            _server.Send(peerId, new RequestMatchAckPacket
            {
                Success = false,
                ErrorMessage = peer?.State == PeerState.InGame
                    ? "Already in a game"
                    : "Not authenticated"
            });
            return;
        }

        var session = CreateSession(packet.Mode);
        JoinSession(peerId, peer, session);

        _server.Send(peerId, new RequestMatchAckPacket
        {
            Success = true,
            MatchId = session.MatchId
        });

        _logger.LogInformation("Peer {PeerId} joined session {MatchId} (mode: {Mode})",
            peerId, session.MatchId, packet.Mode);
    }

    private GameSession CreateSession(GameMode mode)
    {
        var matchId = Guid.NewGuid();
        var sessionLoggerName = $"GameSession[{matchId.ToString("N")[..8]}]";
        var sessionLogger = _loggerFactory.CreateLogger(sessionLoggerName);

        SessionPlayerManager? playerManager = null;

        Action<uint, IPacket> send = (peerId, packet) =>
            _server.SendBytes(peerId, PacketSerializer.Serialize(packet));
        Action<IPacket> broadcast = (packet) =>
        {
            if (playerManager == null) return;
            var data = PacketSerializer.Serialize(packet);
            var peerIds = playerManager.GetPeerIds();
            foreach (var pid in peerIds)
            {
                _server.SendBytes(pid, data);
            }
        };

        playerManager = new SessionPlayerManager(send, broadcast, sessionLogger);

        var session = new GameSession(
            matchId,
            sessionLogger,
            playerManager,
            _eventPublisher,
            _towerBonusService,
            send,
            broadcast);

        session.OnSessionEnded += HandleSessionEnded;
        _sessions[matchId] = session;

        _logger.LogInformation("Created session {MatchId} (mode: {Mode})", matchId, mode);
        return session;
    }

    private void JoinSession(uint peerId, ConnectedPeerInfo peer, GameSession session)
    {
        _peerToSession[peerId] = session.MatchId;
        _connectionManager.SetPeerState(peerId, PeerState.InGame, session.MatchId);
        session.PlayerManager.AddPlayer(peerId, peer);
    }

    private void HandleSessionEnded(GameSession session)
    {
        var peerIds = session.PlayerManager.GetPeerIds();

        foreach (var peerId in peerIds)
        {
            _peerToSession.TryRemove(peerId, out _);
            _connectionManager.SetPeerState(peerId, PeerState.Lobby, null);
            _server.Send(peerId, new ReturnToLobbyPacket());
        }

        _sessions.TryRemove(session.MatchId, out _);
        _logger.LogInformation("Session {MatchId} ended and cleaned up", session.MatchId);
    }

    private void HandlePeerDisconnected(ConnectedPeerInfo peer)
    {
        if (_peerToSession.TryRemove(peer.PeerId, out var sessionId)
            && _sessions.TryGetValue(sessionId, out var session))
        {
            session.PlayerManager.RemovePlayer(peer.PeerId);

            // If no players left, force-end the session
            if (session.PlayerManager.GetAllPlayers().Count == 0)
            {
                session.ForceEnd();
                _sessions.TryRemove(sessionId, out _);
            }
        }
    }

    public IReadOnlyCollection<GameSession> GetAllSessions() => _sessions.Values.ToList();

    public int ActiveSessionCount => _sessions.Count;
}
