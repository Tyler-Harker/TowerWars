using Microsoft.Extensions.Logging;
using TowerWars.Shared.Constants;
using TowerWars.Shared.Protocol;

namespace TowerWars.ZoneServer.Game;

public sealed class SessionPlayer
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

public class SessionPlayerManager
{
    private readonly Dictionary<uint, SessionPlayer> _players = new();
    private readonly object _lock = new();
    private readonly Action<uint, IPacket> _send;
    private readonly Action<IPacket> _broadcast;
    private readonly ILogger _logger;

    public event Action<SessionPlayer>? OnPlayerJoined;
    public event Action<SessionPlayer>? OnPlayerLeft;
    public event Action? OnAllPlayersReady;

    public SessionPlayerManager(
        Action<uint, IPacket> send,
        Action<IPacket> broadcast,
        ILogger logger)
    {
        _send = send;
        _broadcast = broadcast;
        _logger = logger;
    }

    public SessionPlayer AddPlayer(uint peerId, ConnectedPeerInfo peerInfo)
    {
        SessionPlayer player;

        lock (_lock)
        {
            var playerId = (uint)_players.Count + 1;
            player = new SessionPlayer
            {
                PeerId = peerId,
                PlayerId = playerId,
                UserId = peerInfo.UserId,
                CharacterId = peerInfo.CharacterId,
                Name = peerInfo.Name,
                Gold = GameConstants.StartingGold,
                Lives = GameConstants.StartingLives,
                Score = 0,
                TeamId = 0,
                IsReady = false,
                IsConnected = true
            };

            _players[peerId] = player;
            _logger.LogDebug("Player {PlayerId} ({UserId}) added to session", playerId, peerInfo.UserId);
        }

        OnPlayerJoined?.Invoke(player);
        return player;
    }

    public void RemovePlayer(uint peerId)
    {
        lock (_lock)
        {
            if (_players.Remove(peerId, out var player))
            {
                _logger.LogDebug("Player {PlayerId} removed from session", player.PlayerId);
                OnPlayerLeft?.Invoke(player);
            }
        }
    }

    public void SetReady(uint peerId, bool isReady)
    {
        bool allReady = false;

        lock (_lock)
        {
            if (!_players.TryGetValue(peerId, out var player))
                return;

            player.IsReady = isReady;
            _logger.LogDebug("Player {PlayerId} ready: {IsReady}", player.PlayerId, isReady);

            BroadcastPlayerStates();

            allReady = _players.Count > 0 && _players.Values.All(p => p.IsReady);
        }

        // Fire outside lock — handlers call back into this class (GetAllPlayers, etc.)
        if (allReady)
        {
            OnAllPlayersReady?.Invoke();
        }
    }

    public void BroadcastChat(uint peerId, ChatMessagePacket packet)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue(peerId, out var sender))
                return;

            _broadcast(new ChatBroadcastPacket
            {
                Channel = packet.Channel,
                SenderId = sender.PlayerId,
                SenderName = sender.Name,
                Message = packet.Message,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }

    public SessionPlayer? GetPlayer(uint peerId)
    {
        lock (_lock)
        {
            return _players.TryGetValue(peerId, out var player) ? player : null;
        }
    }

    public SessionPlayer? GetPlayerByPlayerId(uint playerId)
    {
        lock (_lock)
        {
            return _players.Values.FirstOrDefault(p => p.PlayerId == playerId);
        }
    }

    public IReadOnlyList<SessionPlayer> GetAllPlayers()
    {
        lock (_lock)
        {
            return _players.Values.ToList();
        }
    }

    public HashSet<uint> GetPeerIds()
    {
        lock (_lock)
        {
            return _players.Keys.ToHashSet();
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

            _broadcast(new StateSnapshotPacket
            {
                Tick = 0,
                Entities = [],
                Players = states
            });
        }
    }
}
