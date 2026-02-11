using Microsoft.Extensions.Logging;
using TowerWars.Shared.Constants;
using TowerWars.Shared.Protocol;
using TowerWars.ZoneServer.Networking;
using TowerWars.ZoneServer.Services;

namespace TowerWars.ZoneServer.Game;

public class PlayerManager
{
    private readonly ILogger<PlayerManager> _logger;
    private readonly ENetServer _server;
    private readonly ITokenValidationService _tokenService;
    private readonly IEventPublisher _eventPublisher;
    private readonly Dictionary<uint, ServerPlayer> _players = new();
    private readonly object _lock = new();

    public event Action<ServerPlayer>? OnPlayerJoined;
    public event Action<ServerPlayer>? OnPlayerLeft;
    public event Action? OnAllPlayersReady;

    public PlayerManager(
        ILogger<PlayerManager> logger,
        ENetServer server,
        ITokenValidationService tokenService,
        IEventPublisher eventPublisher)
    {
        _logger = logger;
        _server = server;
        _tokenService = tokenService;
        _eventPublisher = eventPublisher;

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
            var playerId = (uint)_players.Count + 1;
            var player = new ServerPlayer
            {
                PeerId = peerId,
                PlayerId = playerId,
                UserId = validation.Value.UserId,
                CharacterId = validation.Value.CharacterId,
                Name = $"Player{playerId}",
                Gold = GameConstants.StartingGold,
                Lives = GameConstants.StartingLives,
                Score = 0,
                TeamId = 0,
                IsReady = false,
                IsConnected = true
            };

            _players[peerId] = player;

            _server.Send(peerId, new ConnectAckPacket
            {
                PlayerId = playerId,
                ServerTick = 0,
                TickRate = GameConstants.DefaultTickRate
            });

            _server.Send(peerId, new AuthResponsePacket { Success = true });

            _logger.LogInformation("Player {PlayerId} ({UserId}) connected", playerId, validation.Value.UserId);

            OnPlayerJoined?.Invoke(player);
        }
    }

    public void HandlePing(uint peerId, long clientTime)
    {
        var serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _server.Send(peerId, new PongPacket { ClientTime = clientTime, ServerTime = serverTime });
    }

    public void SetReady(uint peerId, bool isReady)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue(peerId, out var player))
                return;

            player.IsReady = isReady;
            _logger.LogDebug("Player {PlayerId} ready: {IsReady}", player.PlayerId, isReady);

            BroadcastPlayerStates();

            if (_players.Count > 0 && _players.Values.All(p => p.IsReady))
            {
                OnAllPlayersReady?.Invoke();
            }
        }
    }

    public void BroadcastChat(uint peerId, ChatMessagePacket packet)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue(peerId, out var sender))
                return;

            var broadcast = new ChatBroadcastPacket
            {
                Channel = packet.Channel,
                SenderId = sender.PlayerId,
                SenderName = sender.Name,
                Message = packet.Message,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _server.Broadcast(broadcast);
        }
    }

    public ServerPlayer? GetPlayer(uint peerId)
    {
        lock (_lock)
        {
            return _players.TryGetValue(peerId, out var player) ? player : null;
        }
    }

    public ServerPlayer? GetPlayerByPlayerId(uint playerId)
    {
        lock (_lock)
        {
            return _players.Values.FirstOrDefault(p => p.PlayerId == playerId);
        }
    }

    public IReadOnlyList<ServerPlayer> GetAllPlayers()
    {
        lock (_lock)
        {
            return _players.Values.ToList();
        }
    }

    public void ModifyGold(uint playerId, int amount)
    {
        lock (_lock)
        {
            var player = _players.Values.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null)
            {
                player.Gold = Math.Max(0, player.Gold + amount);
            }
        }
    }

    public void ModifyLives(uint playerId, int amount)
    {
        lock (_lock)
        {
            var player = _players.Values.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null)
            {
                player.Lives = Math.Max(0, player.Lives + amount);
            }
        }
    }

    public void AddScore(uint playerId, int score)
    {
        lock (_lock)
        {
            var player = _players.Values.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null)
            {
                player.Score += score;
            }
        }
    }

    public void BroadcastPlayerStates()
    {
        lock (_lock)
        {
            var states = _players.Values.Select(p => new PlayerState
            {
                PlayerId = p.PlayerId,
                Name = p.Name,
                Gold = p.Gold,
                Lives = p.Lives,
                Score = p.Score,
                TeamId = p.TeamId,
                IsReady = p.IsReady,
                IsConnected = p.IsConnected
            }).ToArray();

            var snapshot = new StateSnapshotPacket
            {
                Tick = 0,
                Entities = [],
                Players = states
            };

            _server.Broadcast(snapshot);
        }
    }

    private void HandlePeerConnected(uint peerId)
    {
        _logger.LogDebug("Peer {PeerId} connected, awaiting authentication", peerId);
    }

    private void HandlePeerDisconnected(uint peerId, string reason)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(peerId, out var player))
            {
                _players.Remove(peerId);
                _logger.LogInformation("Player {PlayerId} disconnected: {Reason}", player.PlayerId, reason);
                OnPlayerLeft?.Invoke(player);
            }
        }
    }
}

public sealed class ServerPlayer
{
    public uint PeerId { get; init; }
    public uint PlayerId { get; init; }
    public Guid UserId { get; init; }
    public Guid CharacterId { get; init; }
    public required string Name { get; set; }
    public int Gold { get; set; }
    public int Lives { get; set; }
    public int Score { get; set; }
    public byte TeamId { get; set; }
    public bool IsReady { get; set; }
    public bool IsConnected { get; set; }
    public uint LastProcessedInputSequence { get; set; }
}
