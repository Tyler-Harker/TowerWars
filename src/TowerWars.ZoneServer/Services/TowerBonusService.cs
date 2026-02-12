using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TowerWars.Shared.DTOs;

namespace TowerWars.ZoneServer.Services;

public interface ITowerBonusService
{
    Task<TowerBonusSummaryDto> GetBonusesAsync(Guid towerId);
    Task<WeaponAttackStyleDto?> GetWeaponAttackStyleAsync(Guid towerId);
    void InvalidateCache(Guid towerId);
}

public class TowerBonusService : ITowerBonusService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TowerBonusService> _logger;
    private readonly string _authServiceUrl;

    private readonly ConcurrentDictionary<Guid, CachedBonus> _bonusCache = new();
    private readonly ConcurrentDictionary<Guid, CachedWeapon> _weaponCache = new();

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

    public async Task<TowerBonusSummaryDto> GetBonusesAsync(Guid towerId)
    {
        if (_bonusCache.TryGetValue(towerId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached.Bonuses;
        }

        try
        {
            var url = $"{_authServiceUrl}/internal/towers/{towerId}/bonuses";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch bonuses for tower {TowerId}: {Status}",
                    towerId, response.StatusCode);
                return CreateEmptyBonuses();
            }

            var bonuses = await response.Content.ReadFromJsonAsync<TowerBonusSummaryDto>();
            if (bonuses == null)
                return CreateEmptyBonuses();

            _bonusCache[towerId] = new CachedBonus(bonuses, DateTime.UtcNow + CacheExpiry);
            return bonuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bonuses for tower {TowerId}", towerId);
            return CreateEmptyBonuses();
        }
    }

    public async Task<WeaponAttackStyleDto?> GetWeaponAttackStyleAsync(Guid towerId)
    {
        if (_weaponCache.TryGetValue(towerId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached.Weapon;
        }

        try
        {
            var url = $"{_authServiceUrl}/internal/towers/{towerId}/equipment/weapon";
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _weaponCache[towerId] = new CachedWeapon(null, DateTime.UtcNow + CacheExpiry);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch weapon for tower {TowerId}: {Status}",
                    towerId, response.StatusCode);
                return null;
            }

            var weapon = await response.Content.ReadFromJsonAsync<WeaponAttackStyleDto>();
            _weaponCache[towerId] = new CachedWeapon(weapon, DateTime.UtcNow + CacheExpiry);
            return weapon;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weapon for tower {TowerId}", towerId);
            return null;
        }
    }

    public void InvalidateCache(Guid towerId)
    {
        _bonusCache.TryRemove(towerId, out _);
        _weaponCache.TryRemove(towerId, out _);
        _logger.LogDebug("Invalidated cache for tower {TowerId}", towerId);
    }

    private static TowerBonusSummaryDto CreateEmptyBonuses() => new(
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, new Dictionary<TowerBonusType, decimal>()
    );

    private sealed record CachedBonus(TowerBonusSummaryDto Bonuses, DateTime ExpiresAt);
    private sealed record CachedWeapon(WeaponAttackStyleDto? Weapon, DateTime ExpiresAt);
}

/// <summary>
/// Local tower bonus service for development that returns empty bonuses without making HTTP calls.
/// </summary>
public class LocalTowerBonusService : ITowerBonusService
{
    public Task<TowerBonusSummaryDto> GetBonusesAsync(Guid towerId)
    {
        return Task.FromResult(new TowerBonusSummaryDto(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, new Dictionary<TowerBonusType, decimal>()
        ));
    }

    public Task<WeaponAttackStyleDto?> GetWeaponAttackStyleAsync(Guid towerId)
    {
        return Task.FromResult<WeaponAttackStyleDto?>(null);
    }

    public void InvalidateCache(Guid towerId) { }
}
