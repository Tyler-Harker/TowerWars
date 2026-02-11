using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TowerWars.Shared.Constants;
using TowerWars.Shared.DTOs;

namespace TowerWars.ZoneServer.Services;

public interface ITowerBonusService
{
    Task<TowerBonusSummaryDto> GetBonusesAsync(Guid userId, TowerType towerType);
    Task<WeaponAttackStyleDto?> GetWeaponAttackStyleAsync(Guid userId, TowerType towerType);
    void InvalidateCache(Guid userId);
}

public class TowerBonusService : ITowerBonusService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TowerBonusService> _logger;
    private readonly string _authServiceUrl;

    private readonly ConcurrentDictionary<(Guid, TowerType), CachedBonus> _bonusCache = new();
    private readonly ConcurrentDictionary<(Guid, TowerType), CachedWeapon> _weaponCache = new();

    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    public TowerBonusService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TowerBonusService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _authServiceUrl = configuration["Services:Auth"] ?? "http://localhost:5001";
    }

    public async Task<TowerBonusSummaryDto> GetBonusesAsync(Guid userId, TowerType towerType)
    {
        var key = (userId, towerType);

        if (_bonusCache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached.Bonuses;
        }

        try
        {
            var url = $"{_authServiceUrl}/internal/towers/{userId}/{(int)towerType}/bonuses";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch bonuses for user {UserId}, tower {TowerType}: {Status}",
                    userId, towerType, response.StatusCode);
                return CreateEmptyBonuses();
            }

            var bonuses = await response.Content.ReadFromJsonAsync<TowerBonusSummaryDto>();
            if (bonuses == null)
                return CreateEmptyBonuses();

            _bonusCache[key] = new CachedBonus(bonuses, DateTime.UtcNow + CacheExpiry);
            return bonuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bonuses for user {UserId}, tower {TowerType}",
                userId, towerType);
            return CreateEmptyBonuses();
        }
    }

    public async Task<WeaponAttackStyleDto?> GetWeaponAttackStyleAsync(Guid userId, TowerType towerType)
    {
        var key = (userId, towerType);

        if (_weaponCache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached.Weapon;
        }

        try
        {
            var url = $"{_authServiceUrl}/internal/towers/{userId}/{(int)towerType}/equipment/weapon";
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _weaponCache[key] = new CachedWeapon(null, DateTime.UtcNow + CacheExpiry);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch weapon for user {UserId}, tower {TowerType}: {Status}",
                    userId, towerType, response.StatusCode);
                return null;
            }

            var weapon = await response.Content.ReadFromJsonAsync<WeaponAttackStyleDto>();
            _weaponCache[key] = new CachedWeapon(weapon, DateTime.UtcNow + CacheExpiry);
            return weapon;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weapon for user {UserId}, tower {TowerType}",
                userId, towerType);
            return null;
        }
    }

    public void InvalidateCache(Guid userId)
    {
        var keysToRemove = _bonusCache.Keys.Where(k => k.Item1 == userId).ToList();
        foreach (var key in keysToRemove)
        {
            _bonusCache.TryRemove(key, out _);
        }

        var weaponKeysToRemove = _weaponCache.Keys.Where(k => k.Item1 == userId).ToList();
        foreach (var key in weaponKeysToRemove)
        {
            _weaponCache.TryRemove(key, out _);
        }

        _logger.LogDebug("Invalidated cache for user {UserId}", userId);
    }

    private static TowerBonusSummaryDto CreateEmptyBonuses() => new(
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, new Dictionary<TowerBonusType, decimal>()
    );

    private sealed record CachedBonus(TowerBonusSummaryDto Bonuses, DateTime ExpiresAt);
    private sealed record CachedWeapon(WeaponAttackStyleDto? Weapon, DateTime ExpiresAt);
}
