using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using TowerWars.Shared.Constants;

namespace TowerWars.Client.Data;

public class PlayerData
{
    private const string SavePath = "user://player_data.json";

    [JsonPropertyName("towers")]
    public List<TowerData> Towers { get; set; } = new();

    [JsonPropertyName("highest_tier_unlocked")]
    public int HighestTierUnlocked { get; set; } = 1;

    [JsonPropertyName("gold")]
    public int Gold { get; set; } = 0;

    public static PlayerData Load()
    {
        if (!Godot.FileAccess.FileExists(SavePath))
        {
            GD.Print("No save file found, creating new player data");
            var newData = CreateDefault();
            newData.Save();
            return newData;
        }

        try
        {
            using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
            var json = file.GetAsText();
            var data = JsonSerializer.Deserialize<PlayerData>(json);
            GD.Print($"Loaded player data: {data?.Towers.Count} towers, tier {data?.HighestTierUnlocked} unlocked");
            return data ?? CreateDefault();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"Failed to load player data: {ex.Message}");
            return CreateDefault();
        }
    }

    public void Save()
    {
        try
        {
            using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            file.StoreString(json);
            GD.Print("Player data saved");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"Failed to save player data: {ex.Message}");
        }
    }

    public static PlayerData CreateDefault()
    {
        // Towers are created server-side, local data is synced from server
        return new PlayerData
        {
            Towers = new List<TowerData>(),
            HighestTierUnlocked = 1,
            Gold = 0
        };
    }

    public void UnlockNextTier()
    {
        HighestTierUnlocked++;
        Save();
    }
}

public class TowerData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("weapon_type")]
    public WeaponType WeaponType { get; set; }

    [JsonPropertyName("damage_type")]
    public DamageType DamageType { get; set; } = DamageType.Physical;

    [JsonPropertyName("weapon_level")]
    public int WeaponLevel { get; set; }

    [JsonPropertyName("experience")]
    public long Experience { get; set; }

    [JsonPropertyName("skill_points")]
    public int SkillPoints { get; set; }

    public string GetDisplayName()
    {
        return $"{Name} (Lvl {WeaponLevel} {DamageType} {WeaponType})";
    }
}

public static class TierInfo
{
    public static readonly TierData[] Tiers = new[]
    {
        new TierData(1, "Grasslands", "Easy enemies, perfect for beginners", 10),
        new TierData(2, "Forest", "Faster enemies with more health", 15),
        new TierData(3, "Desert", "Armored enemies resist physical damage", 20),
        new TierData(4, "Swamp", "Enemies regenerate health over time", 25),
        new TierData(5, "Mountains", "Flying enemies bypass ground defenses", 30),
        new TierData(6, "Volcano", "Fire-resistant enemies with high damage", 35),
        new TierData(7, "Frozen Wastes", "Enemies slow your towers", 40),
        new TierData(8, "Shadow Realm", "Invisible enemies appear suddenly", 45),
        new TierData(9, "Dragon's Lair", "Boss tier - face the dragon!", 50),
        new TierData(10, "The Void", "Endless mode - survive as long as you can", 0),
    };
}

public record TierData(int Tier, string Name, string Description, int WaveCount);
