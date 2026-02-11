using StackExchange.Redis;
using TowerWars.Shared.DTOs;

namespace TowerWars.Gateway.Services;

public interface IZoneRegistryService
{
    Task<ZoneInfo?> GetAvailableZoneAsync();
    Task<ZoneInfo?> GetZoneByIdAsync(string zoneId);
    Task RegisterZoneAsync(ZoneInfo zone);
    Task UnregisterZoneAsync(string zoneId);
    Task UpdateZonePlayerCountAsync(string zoneId, int playerCount);
}

public sealed record ZoneInfo(
    string ZoneId,
    string Address,
    int Port,
    int PlayerCount,
    int Capacity,
    DateTime LastHeartbeat
);

public sealed class ZoneRegistryService : IZoneRegistryService
{
    private readonly IConnectionMultiplexer _redis;
    private const string ZonesHashKey = "zones:active";

    public ZoneRegistryService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<ZoneInfo?> GetAvailableZoneAsync()
    {
        var db = _redis.GetDatabase();
        var zones = await db.HashGetAllAsync(ZonesHashKey);

        ZoneInfo? bestZone = null;
        var lowestLoad = float.MaxValue;

        foreach (var entry in zones)
        {
            var zone = System.Text.Json.JsonSerializer.Deserialize<ZoneInfo>(entry.Value.ToString());
            if (zone == null || zone.PlayerCount >= zone.Capacity) continue;

            var load = (float)zone.PlayerCount / zone.Capacity;
            if (load < lowestLoad)
            {
                lowestLoad = load;
                bestZone = zone;
            }
        }

        return bestZone;
    }

    public async Task<ZoneInfo?> GetZoneByIdAsync(string zoneId)
    {
        var db = _redis.GetDatabase();
        var data = await db.HashGetAsync(ZonesHashKey, zoneId);

        if (data.IsNullOrEmpty)
            return null;

        return System.Text.Json.JsonSerializer.Deserialize<ZoneInfo>(data.ToString());
    }

    public async Task RegisterZoneAsync(ZoneInfo zone)
    {
        var db = _redis.GetDatabase();
        var json = System.Text.Json.JsonSerializer.Serialize(zone);
        await db.HashSetAsync(ZonesHashKey, zone.ZoneId, json);
    }

    public async Task UnregisterZoneAsync(string zoneId)
    {
        var db = _redis.GetDatabase();
        await db.HashDeleteAsync(ZonesHashKey, zoneId);
    }

    public async Task UpdateZonePlayerCountAsync(string zoneId, int playerCount)
    {
        var zone = await GetZoneByIdAsync(zoneId);
        if (zone == null) return;

        var updated = zone with { PlayerCount = playerCount, LastHeartbeat = DateTime.UtcNow };
        await RegisterZoneAsync(updated);
    }
}
