using StackExchange.Redis;

namespace TowerWars.Gateway.Services;

public interface ISessionService
{
    Task<SessionInfo?> GetSessionAsync(Guid userId);
    Task SetSessionAsync(Guid userId, SessionInfo session);
    Task DeleteSessionAsync(Guid userId);
}

public sealed record SessionInfo(
    Guid UserId,
    Guid CharacterId,
    string? ZoneId,
    DateTime LastActivity
);

public sealed class SessionService : ISessionService
{
    private readonly IConnectionMultiplexer _redis;
    private const string SessionKeyPrefix = "session:user:";
    private static readonly TimeSpan SessionExpiry = TimeSpan.FromHours(24);

    public SessionService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<SessionInfo?> GetSessionAsync(Guid userId)
    {
        var db = _redis.GetDatabase();
        var data = await db.StringGetAsync(SessionKeyPrefix + userId);

        if (data.IsNullOrEmpty)
            return null;

        return System.Text.Json.JsonSerializer.Deserialize<SessionInfo>(data.ToString());
    }

    public async Task SetSessionAsync(Guid userId, SessionInfo session)
    {
        var db = _redis.GetDatabase();
        var json = System.Text.Json.JsonSerializer.Serialize(session);
        await db.StringSetAsync(SessionKeyPrefix + userId, json, SessionExpiry);
    }

    public async Task DeleteSessionAsync(Guid userId)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(SessionKeyPrefix + userId);
    }
}
