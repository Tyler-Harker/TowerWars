using StackExchange.Redis;
using TowerWars.Shared.Constants;
using TowerWars.Shared.DTOs;
using TowerWars.Shared.Protocol;

namespace TowerWars.WorldManager.Services;

public interface IMatchmakingService
{
    Task<Guid> EnqueueAsync(Guid userId, GameMode mode, int eloRating);
    Task<MatchmakingStatusResponse> GetStatusAsync(Guid ticketId);
    Task<bool> CancelAsync(Guid ticketId);
    Task ProcessQueueAsync();
}

public sealed record MatchmakingTicket(
    Guid TicketId,
    Guid UserId,
    GameMode Mode,
    int EloRating,
    DateTime EnqueuedAt
);

public sealed class MatchmakingService : IMatchmakingService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IZoneOrchestrator _zoneOrchestrator;
    private readonly ILogger<MatchmakingService> _logger;

    private const string PvpQueueKey = "matchmaking:pvp:queue";
    private const string CoopQueueKey = "matchmaking:coop:queue";
    private const string TicketKeyPrefix = "matchmaking:ticket:";
    private const string MatchKeyPrefix = "matchmaking:match:";

    public MatchmakingService(
        IConnectionMultiplexer redis,
        IZoneOrchestrator zoneOrchestrator,
        ILogger<MatchmakingService> logger)
    {
        _redis = redis;
        _zoneOrchestrator = zoneOrchestrator;
        _logger = logger;
    }

    public async Task<Guid> EnqueueAsync(Guid userId, GameMode mode, int eloRating)
    {
        var ticketId = Guid.NewGuid();
        var ticket = new MatchmakingTicket(ticketId, userId, mode, eloRating, DateTime.UtcNow);

        var db = _redis.GetDatabase();
        var json = System.Text.Json.JsonSerializer.Serialize(ticket);
        await db.StringSetAsync(TicketKeyPrefix + ticketId, json, TimeSpan.FromMinutes(5));

        var queueKey = mode == GameMode.PvP ? PvpQueueKey : CoopQueueKey;
        var score = eloRating * 1000000 + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.SortedSetAddAsync(queueKey, ticketId.ToString(), score);

        _logger.LogDebug("Enqueued ticket {TicketId} for user {UserId} in {Mode} queue",
            ticketId, userId, mode);

        return ticketId;
    }

    public async Task<MatchmakingStatusResponse> GetStatusAsync(Guid ticketId)
    {
        var db = _redis.GetDatabase();
        var ticketJson = await db.StringGetAsync(TicketKeyPrefix + ticketId);

        if (ticketJson.IsNullOrEmpty)
        {
            return new MatchmakingStatusResponse(
                ticketId,
                MatchmakingStatus.Expired,
                null, null, null
            );
        }

        var ticket = System.Text.Json.JsonSerializer.Deserialize<MatchmakingTicket>(ticketJson.ToString());
        if (ticket == null)
        {
            return new MatchmakingStatusResponse(ticketId, MatchmakingStatus.Expired, null, null, null);
        }

        var matchJson = await db.StringGetAsync(MatchKeyPrefix + ticketId);
        if (!matchJson.IsNullOrEmpty)
        {
            var match = System.Text.Json.JsonSerializer.Deserialize<MatchFoundInfo>(matchJson.ToString());
            return new MatchmakingStatusResponse(ticketId, MatchmakingStatus.Found, null, null, match);
        }

        var queueKey = ticket.Mode == GameMode.PvP ? PvpQueueKey : CoopQueueKey;
        var rank = await db.SortedSetRankAsync(queueKey, ticketId.ToString());

        if (!rank.HasValue)
        {
            return new MatchmakingStatusResponse(ticketId, MatchmakingStatus.Cancelled, null, null, null);
        }

        var estimatedWait = Math.Max(10, (int)(rank.Value * 5));

        return new MatchmakingStatusResponse(
            ticketId,
            MatchmakingStatus.Queued,
            (int)rank.Value + 1,
            estimatedWait,
            null
        );
    }

    public async Task<bool> CancelAsync(Guid ticketId)
    {
        var db = _redis.GetDatabase();

        await db.KeyDeleteAsync(TicketKeyPrefix + ticketId);
        await db.SortedSetRemoveAsync(PvpQueueKey, ticketId.ToString());
        await db.SortedSetRemoveAsync(CoopQueueKey, ticketId.ToString());

        _logger.LogDebug("Cancelled ticket {TicketId}", ticketId);
        return true;
    }

    public async Task ProcessQueueAsync()
    {
        await ProcessQueueForModeAsync(GameMode.PvP, PvpQueueKey, 2);
        await ProcessQueueForModeAsync(GameMode.Coop, CoopQueueKey, 2);
    }

    private async Task ProcessQueueForModeAsync(GameMode mode, string queueKey, int minPlayers)
    {
        var db = _redis.GetDatabase();
        var entries = await db.SortedSetRangeByRankWithScoresAsync(queueKey, 0, 15);

        if (entries.Length < minPlayers)
            return;

        var tickets = new List<MatchmakingTicket>();

        foreach (var entry in entries)
        {
            var ticketId = Guid.Parse(entry.Element.ToString());
            var ticketJson = await db.StringGetAsync(TicketKeyPrefix + ticketId);

            if (ticketJson.IsNullOrEmpty)
            {
                await db.SortedSetRemoveAsync(queueKey, entry.Element);
                continue;
            }

            var ticket = System.Text.Json.JsonSerializer.Deserialize<MatchmakingTicket>(ticketJson.ToString());
            if (ticket != null)
            {
                tickets.Add(ticket);
            }
        }

        var groups = GroupByElo(tickets, GameConstants.MaxEloGap);

        foreach (var group in groups.Where(g => g.Count >= minPlayers))
        {
            var matchPlayers = group.Take(mode == GameMode.PvP ? 2 : 4).ToList();
            await CreateMatchAsync(mode, matchPlayers);

            foreach (var ticket in matchPlayers)
            {
                await db.SortedSetRemoveAsync(queueKey, ticket.TicketId.ToString());
            }
        }
    }

    private List<List<MatchmakingTicket>> GroupByElo(List<MatchmakingTicket> tickets, int maxGap)
    {
        var sorted = tickets.OrderBy(t => t.EloRating).ToList();
        var groups = new List<List<MatchmakingTicket>>();

        foreach (var ticket in sorted)
        {
            var addedToGroup = false;
            foreach (var group in groups)
            {
                var avgElo = group.Average(t => t.EloRating);
                if (Math.Abs(ticket.EloRating - avgElo) <= maxGap)
                {
                    group.Add(ticket);
                    addedToGroup = true;
                    break;
                }
            }

            if (!addedToGroup)
            {
                groups.Add([ticket]);
            }
        }

        return groups;
    }

    private async Task CreateMatchAsync(GameMode mode, List<MatchmakingTicket> players)
    {
        var zone = await _zoneOrchestrator.GetAvailableZoneAsync(mode);
        if (zone == null)
        {
            _logger.LogWarning("No zone available for match");
            return;
        }

        var matchId = Guid.NewGuid();
        var connectionToken = Guid.NewGuid().ToString("N");

        var matchInfo = new MatchFoundInfo(
            matchId,
            zone.Address,
            zone.Port,
            connectionToken
        );

        var db = _redis.GetDatabase();

        foreach (var ticket in players)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(matchInfo);
            await db.StringSetAsync(MatchKeyPrefix + ticket.TicketId, json, TimeSpan.FromMinutes(2));
        }

        _logger.LogInformation("Created match {MatchId} for {PlayerCount} players on zone {ZoneId}",
            matchId, players.Count, zone.ZoneId);
    }
}
