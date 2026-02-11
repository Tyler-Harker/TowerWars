using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TowerWars.Auth.Data;
using TowerWars.Shared.Constants;

namespace TowerWars.Auth.Services;

public sealed class TowerXpConsumer : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TowerXpConsumer> _logger;

    private const string StreamKey = "stream:game-events";
    private const string ConsumerGroup = "auth-tower-xp";
    private const string ConsumerName = "auth-xp-worker";

    public TowerXpConsumer(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        ILogger<TowerXpConsumer> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync();

        _logger.LogInformation("Tower XP consumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEventsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing tower XP events");
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
        var progressionService = scope.ServiceProvider.GetRequiredService<ITowerProgressionService>();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IEquipmentService>();

        foreach (var entry in entries)
        {
            try
            {
                await ProcessEventAsync(entry, progressionService, equipmentService);
                await db.StreamAcknowledgeAsync(StreamKey, ConsumerGroup, entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process event {Id}", entry.Id);
            }
        }
    }

    private async Task ProcessEventAsync(
        StreamEntry entry,
        ITowerProgressionService progressionService,
        IEquipmentService equipmentService)
    {
        var data = entry.Values.FirstOrDefault(v => v.Name == "data").Value.ToString();
        if (string.IsNullOrEmpty(data)) return;

        var json = JsonDocument.Parse(data);
        var root = json.RootElement;

        if (!root.TryGetProperty("EventType", out var eventTypeProp)) return;
        var eventType = eventTypeProp.GetString();

        switch (eventType)
        {
            case "tower.xp_gained":
                await HandleTowerXpGained(root, progressionService);
                break;

            case "item.dropped":
                await HandleItemDropped(root, equipmentService);
                break;
        }
    }

    private async Task HandleTowerXpGained(
        JsonElement root,
        ITowerProgressionService progressionService)
    {
        if (!root.TryGetProperty("PlayerId", out var playerIdProp)) return;
        if (!root.TryGetProperty("TowerType", out var towerTypeProp)) return;
        if (!root.TryGetProperty("XpAmount", out var xpAmountProp)) return;

        var playerId = playerIdProp.GetGuid();
        var towerType = towerTypeProp.GetByte();
        var xpAmount = xpAmountProp.GetInt32();

        _logger.LogDebug("Processing tower XP: Player={PlayerId}, Tower={TowerType}, XP={Xp}",
            playerId, towerType, xpAmount);

        await progressionService.AddTowerXpAsync(playerId, towerType, xpAmount);
    }

    private async Task HandleItemDropped(
        JsonElement root,
        IEquipmentService equipmentService)
    {
        if (!root.TryGetProperty("PlayerId", out var playerIdProp)) return;
        if (!root.TryGetProperty("Rarity", out var rarityProp)) return;
        if (!root.TryGetProperty("Source", out var sourceProp)) return;

        var playerId = playerIdProp.GetGuid();
        var rarity = (Shared.DTOs.ItemRarity)rarityProp.GetByte();
        var source = sourceProp.GetString() ?? "unknown";

        Guid? matchId = null;
        if (root.TryGetProperty("MatchId", out var matchIdProp))
            matchId = matchIdProp.GetGuid();

        _logger.LogDebug("Processing item drop: Player={PlayerId}, Rarity={Rarity}, Source={Source}",
            playerId, rarity, source);

        await equipmentService.GenerateItemAsync(new Shared.DTOs.GenerateItemRequest(
            playerId,
            rarity,
            null,
            source,
            matchId
        ));
    }
}
