using StackExchange.Redis;

namespace TowerWars.Auth.Services;

public interface ISessionCacheService
{
    Task<Guid?> GetAsync(string tokenHash);
    Task SetAsync(string tokenHash, Guid userId, TimeSpan expiry);
    Task InvalidateAsync(string tokenHash);
}

public sealed class SessionCacheService : ISessionCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "session:";

    public SessionCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<Guid?> GetAsync(string tokenHash)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(KeyPrefix + tokenHash);

        if (value.IsNullOrEmpty)
            return null;

        return Guid.TryParse(value.ToString(), out var userId) ? userId : null;
    }

    public async Task SetAsync(string tokenHash, Guid userId, TimeSpan expiry)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(KeyPrefix + tokenHash, userId.ToString(), expiry);
    }

    public async Task InvalidateAsync(string tokenHash)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(KeyPrefix + tokenHash);
    }
}
