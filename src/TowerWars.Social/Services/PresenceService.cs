using StackExchange.Redis;
using TowerWars.Shared.DTOs;

namespace TowerWars.Social.Services;

public interface IPresenceService
{
    Task SetPresenceAsync(Guid userId, PresenceStatus status);
    Task<PresenceStatus> GetPresenceAsync(Guid userId);
    Task<Dictionary<Guid, PresenceStatus>> GetPresencesAsync(IEnumerable<Guid> userIds);
    Task SetLastOnlineAsync(Guid userId);
}

public sealed class PresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer _redis;
    private const string PresenceKeyPrefix = "presence:";
    private const string LastOnlineKeyPrefix = "last_online:";
    private static readonly TimeSpan PresenceExpiry = TimeSpan.FromMinutes(5);

    public PresenceService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task SetPresenceAsync(Guid userId, PresenceStatus status)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(PresenceKeyPrefix + userId, (int)status, PresenceExpiry);

        if (status != PresenceStatus.Offline)
        {
            await SetLastOnlineAsync(userId);
        }
    }

    public async Task<PresenceStatus> GetPresenceAsync(Guid userId)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(PresenceKeyPrefix + userId);

        if (value.IsNullOrEmpty)
            return PresenceStatus.Offline;

        return (PresenceStatus)(int)value;
    }

    public async Task<Dictionary<Guid, PresenceStatus>> GetPresencesAsync(IEnumerable<Guid> userIds)
    {
        var db = _redis.GetDatabase();
        var keys = userIds.Select(id => (RedisKey)(PresenceKeyPrefix + id)).ToArray();
        var values = await db.StringGetAsync(keys);

        var result = new Dictionary<Guid, PresenceStatus>();
        var userIdList = userIds.ToList();

        for (var i = 0; i < userIdList.Count; i++)
        {
            var status = values[i].IsNullOrEmpty
                ? PresenceStatus.Offline
                : (PresenceStatus)(int)values[i];
            result[userIdList[i]] = status;
        }

        return result;
    }

    public async Task SetLastOnlineAsync(Guid userId)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(
            LastOnlineKeyPrefix + userId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        );
    }
}
