using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TowerWars.Persistence.Data;
using TowerWars.Shared.Constants;

namespace TowerWars.Persistence.Services;

public sealed class EventConsumer : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventConsumer> _logger;

    private const string StreamKey = "stream:game-events";
    private const string ConsumerGroup = "persistence";
    private const string ConsumerName = "persistence-worker";

    public EventConsumer(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        ILogger<EventConsumer> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync();

        _logger.LogInformation("Event consumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEventsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing events");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task EnsureConsumerGroupAsync()
    {
        var db = _redis.GetDatabase();

        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamKey, ConsumerGroup, "$", createStream: true);
            _logger.LogInformation("Created consumer group {Group}", ConsumerGroup);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            _logger.LogDebug("Consumer group {Group} already exists", ConsumerGroup);
        }
    }

    private async Task ProcessEventsAsync(CancellationToken ct)
    {
        var db = _redis.GetDatabase();

        var entries = await db.StreamReadGroupAsync(
            StreamKey,
            ConsumerGroup,
            ConsumerName,
            count: 100,
            noAck: false
        );

        if (entries.Length == 0)
        {
            await Task.Delay(100, ct);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PersistenceDbContext>();

        foreach (var entry in entries)
        {
            try
            {
                await ProcessEventAsync(entry, dbContext);
                await db.StreamAcknowledgeAsync(StreamKey, ConsumerGroup, entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process event {Id}", entry.Id);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task ProcessEventAsync(StreamEntry entry, PersistenceDbContext db)
    {
        var data = entry.Values.FirstOrDefault(v => v.Name == "data").Value.ToString();
        if (string.IsNullOrEmpty(data)) return;

        var json = JsonDocument.Parse(data);
        var root = json.RootElement;

        if (!root.TryGetProperty("EventType", out var eventTypeProp)) return;
        var eventType = eventTypeProp.GetString();

        _logger.LogDebug("Processing event: {EventType}", eventType);

        switch (eventType)
        {
            case "match.started":
                await HandleMatchStarted(root, db);
                break;

            case "match.ended":
                await HandleMatchEnded(root, db);
                break;

            case "tower.built":
                await HandleTowerBuilt(root, db);
                break;

            case "unit.killed":
                await HandleUnitKilled(root, db);
                break;

            case "player.damaged":
                await HandlePlayerDamaged(root, db);
                break;

            case "wave.completed":
                await HandleWaveCompleted(root, db);
                break;

            case "tower.xp_gained":
                // Handled by Auth service's TowerXpConsumer
                break;

            case "item.dropped":
                // Handled by Auth service's TowerXpConsumer
                break;
        }

        if (root.TryGetProperty("MatchId", out var matchIdProp))
        {
            var matchId = matchIdProp.GetGuid();
            db.MatchEvents.Add(new MatchEvent
            {
                MatchId = matchId,
                EventType = eventType ?? "unknown",
                EventData = data,
                OccurredAt = DateTime.UtcNow
            });
        }
    }

    private async Task HandleMatchStarted(JsonElement root, PersistenceDbContext db)
    {
        var matchId = root.GetProperty("MatchId").GetGuid();
        var mode = root.GetProperty("Mode").GetString() ?? "Solo";
        var mapId = root.TryGetProperty("MapId", out var mapProp) ? mapProp.GetString() : null;

        var match = new Match
        {
            Id = matchId,
            Mode = mode,
            MapId = mapId,
            StartedAt = DateTime.UtcNow
        };

        if (root.TryGetProperty("PlayerIds", out var playerIds))
        {
            foreach (var playerId in playerIds.EnumerateArray())
            {
                var userId = playerId.GetGuid();
                match.Participants.Add(new MatchParticipant
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId,
                    UserId = userId
                });
            }
        }

        db.Matches.Add(match);
    }

    private async Task HandleMatchEnded(JsonElement root, PersistenceDbContext db)
    {
        var matchId = root.GetProperty("MatchId").GetGuid();
        var result = root.GetProperty("Result").GetString();
        var wavesCompleted = root.TryGetProperty("WavesCompleted", out var waves) ? waves.GetInt32() : 0;
        var duration = root.TryGetProperty("DurationSeconds", out var dur) ? dur.GetSingle() : 0;

        var match = await db.Matches
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null) return;

        match.Result = result;
        match.WavesCompleted = wavesCompleted;
        match.DurationSeconds = duration;
        match.EndedAt = DateTime.UtcNow;

        foreach (var participant in match.Participants)
        {
            var stats = await db.PlayerStats.FindAsync(participant.UserId);
            if (stats == null)
            {
                stats = new PlayerStats { UserId = participant.UserId };
                db.PlayerStats.Add(stats);
            }

            stats.TotalPlayTimeSeconds += (long)duration;
            stats.UpdatedAt = DateTime.UtcNow;

            if (result == "Victory")
            {
                stats.Wins++;
                stats.EloRating += GameConstants.EloKFactor / 2;
                participant.Result = "Victory";
            }
            else if (result == "Defeat")
            {
                stats.Losses++;
                stats.EloRating = Math.Max(0, stats.EloRating - GameConstants.EloKFactor / 2);
                participant.Result = "Defeat";
            }

            if (match.Mode == "Solo" && wavesCompleted > stats.HighestWaveSolo)
            {
                stats.HighestWaveSolo = wavesCompleted;
            }
        }
    }

    private async Task HandleTowerBuilt(JsonElement root, PersistenceDbContext db)
    {
        if (!root.TryGetProperty("PlayerId", out var playerIdProp)) return;
        var playerId = playerIdProp.GetGuid();

        var stats = await db.PlayerStats.FindAsync(playerId);
        if (stats == null)
        {
            stats = new PlayerStats { UserId = playerId };
            db.PlayerStats.Add(stats);
        }

        stats.TotalTowersBuilt++;
        stats.UpdatedAt = DateTime.UtcNow;
    }

    private async Task HandleUnitKilled(JsonElement root, PersistenceDbContext db)
    {
        if (!root.TryGetProperty("PlayerId", out var playerIdProp)) return;
        var playerId = playerIdProp.GetGuid();
        var goldAwarded = root.TryGetProperty("GoldAwarded", out var gold) ? gold.GetInt32() : 0;

        var stats = await db.PlayerStats.FindAsync(playerId);
        if (stats == null)
        {
            stats = new PlayerStats { UserId = playerId };
            db.PlayerStats.Add(stats);
        }

        stats.TotalUnitsKilled++;
        stats.TotalGoldEarned += goldAwarded;
        stats.UpdatedAt = DateTime.UtcNow;
    }

    private Task HandlePlayerDamaged(JsonElement root, PersistenceDbContext db)
    {
        return Task.CompletedTask;
    }

    private async Task HandleWaveCompleted(JsonElement root, PersistenceDbContext db)
    {
        var matchId = root.GetProperty("MatchId").GetGuid();
        var waveNumber = root.TryGetProperty("WaveNumber", out var wave) ? wave.GetInt32() : 0;
        var unitsKilled = root.TryGetProperty("UnitsKilled", out var killed) ? killed.GetInt32() : 0;
        var unitsLeaked = root.TryGetProperty("UnitsLeaked", out var leaked) ? leaked.GetInt32() : 0;

        var match = await db.Matches.FindAsync(matchId);
        if (match != null)
        {
            match.WavesCompleted = waveNumber;
        }

        _logger.LogDebug("Wave {Wave} completed for match {MatchId}: {Killed} killed, {Leaked} leaked",
            waveNumber, matchId, unitsKilled, unitsLeaked);
    }
}
