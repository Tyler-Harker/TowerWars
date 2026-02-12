using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Godot;
using TowerWars.Client.Autoloads;
using TowerWars.Shared.Constants;

namespace TowerWars.Client.Services;

public class ApiClient
{
    private readonly System.Net.Http.HttpClient _httpClient;
    private string? _baseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ApiClient()
    {
        _httpClient = new System.Net.Http.HttpClient();
    }

    public void Configure(string baseUrl, string? authToken = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient.DefaultRequestHeaders.Clear();

        if (!string.IsNullOrEmpty(authToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");
        }
    }

    // === TOWER ENDPOINTS ===

    public async Task<List<ServerTower>> GetTowersAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/towers");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ServerTower>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to get towers: {ex.Message}");
            return new List<ServerTower>();
        }
    }

    public async Task<ServerTower?> CreateTowerAsync(string name, WeaponType weaponType, DamageType damageType = DamageType.Physical)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { name, weaponType, damageType }, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/towers", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ServerTower>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to create tower: {ex.Message}");
            return null;
        }
    }

    public async Task<ExperienceResult?> AddExperienceAsync(Guid towerId, long amount)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { amount }, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/towers/{towerId}/experience", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ExperienceResult>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to add experience: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ServerSkillAllocation>?> AllocateSkillAsync(Guid towerId, string skillId, int points)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { skillId, points }, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/towers/{towerId}/skills", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ServerSkillAllocation>>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to allocate skill: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ServerSkillDefinition>> GetSkillDefinitionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/skills");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ServerSkillDefinition>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to get skill definitions: {ex.Message}");
            return new List<ServerSkillDefinition>();
        }
    }

    // === ITEM ENDPOINTS ===

    public async Task<List<ServerItem>> GetItemsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/items");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ServerItem>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to get items: {ex.Message}");
            return new List<ServerItem>();
        }
    }

    public async Task<ServerItem?> CreateItemAsync(ServerItem item)
    {
        try
        {
            var request = new
            {
                name = item.Name,
                itemType = item.TowerItemType,
                rarity = item.Rarity,
                itemLevel = item.ItemLevel,
                baseStatsJson = item.BaseStatsJson,
                affixesJson = item.AffixesJson
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/items", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ServerItem>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to create item: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> EquipItemAsync(Guid towerId, Guid itemId, string slot)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { itemId, slot }, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/towers/{towerId}/equip", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to equip item: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnequipItemAsync(Guid towerId, string slot)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { slot }, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/towers/{towerId}/unequip", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to unequip item: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteItemAsync(Guid itemId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/items/{itemId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to delete item: {ex.Message}");
            return false;
        }
    }

    // === GAME SESSION ENDPOINTS ===

    public async Task<ServerItemDrop?> ReportEnemyKilledAsync(string sessionId, int tier, float positionX, float positionY)
    {
        try
        {
            var request = new { sessionId, tier, positionX, positionY };
            var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/game/enemy-killed", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<EnemyKilledResponse>(json, JsonOptions);
            return result?.ItemDrop;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to report enemy killed: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ServerItemDrop>> GetItemDropsAsync(string sessionId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/game/item-drops/{sessionId}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ServerItemDrop>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to get item drops: {ex.Message}");
            return new List<ServerItemDrop>();
        }
    }

    public async Task<CollectItemResult?> CollectItemDropAsync(Guid dropId)
    {
        try
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/game/item-drops/{dropId}/collect", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                GD.PrintErr($"Failed to collect item: {response.StatusCode} - {errorText}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CollectItemResult>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to collect item drop: {ex.Message}");
            return null;
        }
    }
}

// Server DTOs
public class ServerTower
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("weaponType")]
    public WeaponType WeaponType { get; set; }

    [JsonPropertyName("damageType")]
    public DamageType DamageType { get; set; } = DamageType.Physical;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("experience")]
    public long Experience { get; set; }

    [JsonPropertyName("skillPoints")]
    public int SkillPoints { get; set; }

    [JsonPropertyName("baseDamage")]
    public float BaseDamage { get; set; }

    [JsonPropertyName("baseAttackSpeed")]
    public float BaseAttackSpeed { get; set; }

    [JsonPropertyName("baseRange")]
    public float BaseRange { get; set; }

    [JsonPropertyName("baseCritChance")]
    public float BaseCritChance { get; set; }

    [JsonPropertyName("baseCritDamage")]
    public float BaseCritDamage { get; set; }

    [JsonPropertyName("skillAllocations")]
    public List<ServerSkillAllocation> SkillAllocations { get; set; } = new();

    [JsonPropertyName("equippedItems")]
    public List<ServerEquippedItem> EquippedItems { get; set; } = new();
}

public class ServerSkillAllocation
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("towerId")]
    public Guid TowerId { get; set; }

    [JsonPropertyName("skillId")]
    public string SkillId { get; set; } = "";

    [JsonPropertyName("points")]
    public int Points { get; set; }
}

public class ServerEquippedItem
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("towerId")]
    public Guid TowerId { get; set; }

    [JsonPropertyName("itemId")]
    public Guid ItemId { get; set; }

    [JsonPropertyName("slot")]
    public string Slot { get; set; } = "";

    [JsonPropertyName("item")]
    public ServerItem? Item { get; set; }
}

public class ServerItem
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("itemType")]
    public TowerItemType TowerItemType { get; set; }

    [JsonPropertyName("rarity")]
    public TowerItemRarity Rarity { get; set; }

    [JsonPropertyName("itemLevel")]
    public int ItemLevel { get; set; }

    [JsonPropertyName("baseStatsJson")]
    public string BaseStatsJson { get; set; } = "{}";

    [JsonPropertyName("affixesJson")]
    public string AffixesJson { get; set; } = "[]";

    [JsonPropertyName("isEquipped")]
    public bool IsEquipped { get; set; }

    [JsonPropertyName("droppedAt")]
    public DateTime DroppedAt { get; set; }

    [JsonPropertyName("collectedAt")]
    public DateTime? CollectedAt { get; set; }
}

public class ServerSkillDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("maxPoints")]
    public int MaxPoints { get; set; }

    [JsonPropertyName("prerequisiteSkillId")]
    public string? PrerequisiteSkillId { get; set; }

    [JsonPropertyName("prerequisitePoints")]
    public int PrerequisitePoints { get; set; }

    [JsonPropertyName("effectType")]
    public string EffectType { get; set; } = "";

    [JsonPropertyName("effectValuePerPoint")]
    public float EffectValuePerPoint { get; set; }

    [JsonPropertyName("damageType")]
    public DamageType? DamageType { get; set; }

    [JsonPropertyName("tierRequired")]
    public int TierRequired { get; set; }
}

public class ExperienceResult
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("experience")]
    public long Experience { get; set; }

    [JsonPropertyName("skillPoints")]
    public int SkillPoints { get; set; }

    [JsonPropertyName("nextLevelExp")]
    public long NextLevelExp { get; set; }
}

// Game session DTOs
public class EnemyKilledResponse
{
    [JsonPropertyName("itemDrop")]
    public ServerItemDrop? ItemDrop { get; set; }
}

public class ServerItemDrop
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("itemType")]
    public TowerItemType ItemType { get; set; }

    [JsonPropertyName("rarity")]
    public TowerItemRarity Rarity { get; set; }

    [JsonPropertyName("itemLevel")]
    public int ItemLevel { get; set; }

    [JsonPropertyName("positionX")]
    public float PositionX { get; set; }

    [JsonPropertyName("positionY")]
    public float PositionY { get; set; }
}

public class CollectItemResult
{
    [JsonPropertyName("itemId")]
    public Guid ItemId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("rarity")]
    public TowerItemRarity Rarity { get; set; }

    [JsonPropertyName("itemLevel")]
    public int ItemLevel { get; set; }
}
