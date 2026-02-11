using System.Security.Cryptography;
using StackExchange.Redis;

namespace TowerWars.Auth.Services;

public interface IConnectionTokenService
{
    Task<string> GenerateTokenAsync(Guid userId, Guid characterId);
    Task<(Guid UserId, Guid CharacterId)?> ValidateTokenAsync(string token);
    Task InvalidateTokenAsync(string token);
}

public sealed class ConnectionTokenService : IConnectionTokenService
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "connection_token:";
    private static readonly TimeSpan TokenExpiry = TimeSpan.FromMinutes(5);

    public ConnectionTokenService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<string> GenerateTokenAsync(Guid userId, Guid characterId)
    {
        var token = GenerateSecureToken();
        var db = _redis.GetDatabase();

        var value = $"{userId}:{characterId}";
        await db.StringSetAsync(KeyPrefix + token, value, TokenExpiry);

        return token;
    }

    public async Task<(Guid UserId, Guid CharacterId)?> ValidateTokenAsync(string token)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(KeyPrefix + token);

        if (value.IsNullOrEmpty)
            return null;

        var parts = value.ToString().Split(':');
        if (parts.Length != 2)
            return null;

        if (!Guid.TryParse(parts[0], out var userId) || !Guid.TryParse(parts[1], out var characterId))
            return null;

        return (userId, characterId);
    }

    public async Task InvalidateTokenAsync(string token)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(KeyPrefix + token);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
