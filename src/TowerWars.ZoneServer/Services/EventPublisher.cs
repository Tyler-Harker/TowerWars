using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace TowerWars.ZoneServer.Services;

public interface IEventPublisher
{
    Task PublishAsync<T>(T gameEvent) where T : class;
}

public sealed class RedisEventPublisher : IEventPublisher
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisEventPublisher> _logger;
    private const string StreamKey = "stream:game-events";

    public RedisEventPublisher(IConnectionMultiplexer redis, ILogger<RedisEventPublisher> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T gameEvent) where T : class
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(gameEvent);

            await db.StreamAddAsync(StreamKey, new NameValueEntry[]
            {
                new("data", json),
                new("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            }, maxLength: 100000, useApproximateMaxLength: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish event to Redis stream");
        }
    }
}

public sealed class NoOpEventPublisher : IEventPublisher
{
    public Task PublishAsync<T>(T gameEvent) where T : class => Task.CompletedTask;
}
