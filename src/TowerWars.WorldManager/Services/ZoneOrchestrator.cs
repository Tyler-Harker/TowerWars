using System.Collections.Concurrent;
using StackExchange.Redis;
using TowerWars.Shared.Protocol;

namespace TowerWars.WorldManager.Services;

public interface IZoneOrchestrator
{
    Task<ZoneInstance?> GetAvailableZoneAsync(GameMode mode);
    Task<ZoneInstance?> CreateInstanceAsync(GameMode mode, IEnumerable<Guid> playerIds, string? mapId = null);
    Task<bool> DestroyInstanceAsync(string instanceId);
    Task RegisterZoneAsync(ZoneInstance zone);
    Task UpdateZoneAsync(string zoneId, int playerCount, IEnumerable<string>? activeMatches = null);
    Task<ZoneInstance?> GetZoneAsync(string zoneId);
    Task<IEnumerable<ZoneInstance>> GetAllZonesAsync();
}

public sealed record ZoneInstance(
    string ZoneId,
    string Address,
    int Port,
    int PlayerCount,
    int Capacity,
    ZoneStatus Status,
    DateTime CreatedAt,
    DateTime LastHeartbeat,
    List<string> ActiveMatches
);

public enum ZoneStatus
{
    Starting,
    Ready,
    Full,
    Draining,
    Terminated
}

public sealed class ZoneOrchestrator : IZoneOrchestrator
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ZoneOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, ZoneInstance> _zones = new();
    private const string ZonesHashKey = "zones:active";

    public ZoneOrchestrator(IConnectionMultiplexer redis, ILogger<ZoneOrchestrator> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<ZoneInstance?> GetAvailableZoneAsync(GameMode mode)
    {
        await SyncFromRedisAsync();

        var availableZones = _zones.Values
            .Where(z => z.Status == ZoneStatus.Ready && z.PlayerCount < z.Capacity)
            .OrderBy(z => (float)z.PlayerCount / z.Capacity)
            .ToList();

        return availableZones.FirstOrDefault();
    }

    public async Task<ZoneInstance?> CreateInstanceAsync(GameMode mode, IEnumerable<Guid> playerIds, string? mapId = null)
    {
        var zone = await GetAvailableZoneAsync(mode);
        if (zone == null)
        {
            _logger.LogWarning("No available zones for mode {Mode}", mode);
            return null;
        }

        _logger.LogInformation("Assigned instance on zone {ZoneId} for {PlayerCount} players",
            zone.ZoneId, playerIds.Count());

        return zone;
    }

    public async Task<bool> DestroyInstanceAsync(string instanceId)
    {
        _logger.LogInformation("Instance {InstanceId} destroyed", instanceId);
        return true;
    }

    public async Task RegisterZoneAsync(ZoneInstance zone)
    {
        _zones[zone.ZoneId] = zone;

        var db = _redis.GetDatabase();
        var json = System.Text.Json.JsonSerializer.Serialize(zone);
        await db.HashSetAsync(ZonesHashKey, zone.ZoneId, json);

        _logger.LogInformation("Zone {ZoneId} registered at {Address}:{Port}",
            zone.ZoneId, zone.Address, zone.Port);
    }

    public async Task UpdateZoneAsync(string zoneId, int playerCount, IEnumerable<string>? activeMatches = null)
    {
        if (!_zones.TryGetValue(zoneId, out var zone))
        {
            await SyncFromRedisAsync();
            if (!_zones.TryGetValue(zoneId, out zone))
                return;
        }

        var status = playerCount >= zone.Capacity ? ZoneStatus.Full : ZoneStatus.Ready;
        var updated = zone with
        {
            PlayerCount = playerCount,
            Status = status,
            LastHeartbeat = DateTime.UtcNow,
            ActiveMatches = activeMatches?.ToList() ?? zone.ActiveMatches
        };

        _zones[zoneId] = updated;

        var db = _redis.GetDatabase();
        var json = System.Text.Json.JsonSerializer.Serialize(updated);
        await db.HashSetAsync(ZonesHashKey, zoneId, json);
    }

    public async Task<ZoneInstance?> GetZoneAsync(string zoneId)
    {
        if (_zones.TryGetValue(zoneId, out var zone))
            return zone;

        await SyncFromRedisAsync();
        return _zones.TryGetValue(zoneId, out zone) ? zone : null;
    }

    public async Task<IEnumerable<ZoneInstance>> GetAllZonesAsync()
    {
        await SyncFromRedisAsync();
        return _zones.Values.ToList();
    }

    private async Task SyncFromRedisAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var entries = await db.HashGetAllAsync(ZonesHashKey);

            foreach (var entry in entries)
            {
                var zone = System.Text.Json.JsonSerializer.Deserialize<ZoneInstance>(entry.Value.ToString());
                if (zone != null)
                {
                    _zones[zone.ZoneId] = zone;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync zones from Redis");
        }
    }
}
