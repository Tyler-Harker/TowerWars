using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TowerWars.Client.Autoloads;
using TowerWars.Client.Services;
using TowerWars.Shared.Constants;

namespace TowerWars.Client.UI;

public partial class TowerDetail : Control
{
    private Button? _backButton;
    private Label? _towerNameLabel;
    private VBoxContainer? _statsContainer;
    private VBoxContainer? _slotsContainer;
    private VBoxContainer? _inventoryList;

    private ServerTower? _tower;
    private List<ServerItem> _playerItems = new();

    public override void _Ready()
    {
        _backButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/Header/BackButton");
        _towerNameLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/Header/TowerNameLabel");
        _statsContainer = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/ContentArea/TowerInfoPanel/VBoxContainer/StatsContainer");
        _slotsContainer = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/ContentArea/EquipmentPanel/VBoxContainer/SlotsContainer");
        _inventoryList = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/ContentArea/InventoryPanel/VBoxContainer/ScrollContainer/InventoryList");

        _backButton?.Connect("pressed", Callable.From(OnBackPressed));

        _tower = GameManager.Instance?.SelectedTower;
        if (_tower == null)
        {
            GD.PrintErr("No tower selected!");
            GetTree().ChangeSceneToFile("res://scenes/dashboard.tscn");
            return;
        }

        LoadData();
    }

    private async void LoadData()
    {
        if (GameManager.Instance?.Api == null || _tower == null) return;

        // Reload tower data to get latest
        var towers = await GameManager.Instance.Api.GetTowersAsync();
        _tower = towers.FirstOrDefault(t => t.Id == _tower.Id) ?? _tower;

        // Load player items
        _playerItems = await GameManager.Instance.Api.GetItemsAsync();

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_tower == null) return;

        // Update tower name
        if (_towerNameLabel != null)
        {
            _towerNameLabel.Text = _tower.Name;
        }

        UpdateStats();
        UpdateEquipmentSlots();
        UpdateInventory();
    }

    private void UpdateStats()
    {
        if (_statsContainer == null || _tower == null) return;

        foreach (var child in _statsContainer.GetChildren())
            child.QueueFree();

        // Get weapon stats from equipped item
        var weaponEquip = _tower.EquippedItems?.FirstOrDefault(e => e.Slot == "weapon");
        var weaponItem = weaponEquip?.Item;

        AddStatRow("Level", _tower.Level.ToString());
        AddStatRow("Experience", $"{_tower.Experience}");
        AddStatRow("Skill Points", _tower.SkillPoints.ToString());
        AddStatRow("", ""); // Spacer

        if (weaponItem != null)
        {
            AddStatRow("Equipped Weapon", weaponItem.Name, GetRarityColor(weaponItem.Rarity));

            // Parse weapon stats from BaseStatsJson
            try
            {
                var stats = JsonDocument.Parse(weaponItem.BaseStatsJson).RootElement;
                if (stats.TryGetProperty("damage", out var dmg))
                    AddStatRow("Damage", dmg.ToString());
                if (stats.TryGetProperty("attackSpeed", out var spd))
                    AddStatRow("Attack Speed", $"{spd}");
                if (stats.TryGetProperty("range", out var rng))
                    AddStatRow("Range", rng.ToString());
            }
            catch { }
        }
        else
        {
            AddStatRow("Weapon", "(none equipped)", new Color(0.5f, 0.5f, 0.5f));
        }
    }

    private void AddStatRow(string label, string value, Color? valueColor = null)
    {
        if (_statsContainer == null) return;

        var row = new HBoxContainer();

        var labelNode = new Label { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        labelNode.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        row.AddChild(labelNode);

        var valueNode = new Label { Text = value };
        valueNode.AddThemeColorOverride("font_color", valueColor ?? new Color(1, 1, 1));
        row.AddChild(valueNode);

        _statsContainer.AddChild(row);
    }

    private void UpdateEquipmentSlots()
    {
        if (_slotsContainer == null || _tower == null) return;

        foreach (var child in _slotsContainer.GetChildren())
            child.QueueFree();

        string[] slots = { "weapon", "upgrade1", "upgrade2" };
        string[] slotNames = { "Weapon", "Upgrade 1", "Upgrade 2" };

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var slotName = slotNames[i];
            var equippedLink = _tower.EquippedItems?.FirstOrDefault(e => e.Slot == slot);
            var equippedItem = equippedLink?.Item;

            var row = new HBoxContainer();
            row.CustomMinimumSize = new Vector2(0, 50);

            var slotLabel = new Label
            {
                Text = $"{slotName}:",
                CustomMinimumSize = new Vector2(100, 0)
            };
            row.AddChild(slotLabel);

            if (equippedItem != null)
            {
                var itemLabel = new Label
                {
                    Text = $"{equippedItem.Name} (iLvl {equippedItem.ItemLevel})",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    TooltipText = BuildItemTooltip(equippedItem),
                    MouseFilter = MouseFilterEnum.Stop // Enable mouse events for tooltip
                };
                itemLabel.AddThemeColorOverride("font_color", GetRarityColor(equippedItem.Rarity));
                row.AddChild(itemLabel);

                var unequipBtn = new Button { Text = "Unequip" };
                var slotCopy = slot;
                unequipBtn.Pressed += () => OnUnequipPressed(slotCopy);
                row.AddChild(unequipBtn);
            }
            else
            {
                var emptyLabel = new Label
                {
                    Text = "(empty)",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                row.AddChild(emptyLabel);
            }

            _slotsContainer.AddChild(row);
        }
    }

    private void UpdateInventory()
    {
        if (_inventoryList == null) return;

        foreach (var child in _inventoryList.GetChildren())
            child.QueueFree();

        var unequippedItems = _playerItems.Where(i => !i.IsEquipped).ToList();

        if (unequippedItems.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "No items in inventory",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _inventoryList.AddChild(emptyLabel);
            return;
        }

        foreach (var item in unequippedItems)
        {
            var row = new HBoxContainer();
            row.CustomMinimumSize = new Vector2(0, 40);
            row.MouseFilter = Control.MouseFilterEnum.Stop; // Enable mouse events for tooltip

            // Find the equipped item in the slot this item would go into
            ServerItem? equippedForComparison = null;
            if (item.TowerItemType == TowerItemType.Weapon)
            {
                equippedForComparison = _tower.EquippedItems?
                    .FirstOrDefault(e => e.Slot == "weapon")?.Item;
            }
            else
            {
                // For upgrades, compare against the first occupied upgrade slot
                equippedForComparison = _tower.EquippedItems?
                    .FirstOrDefault(e => e.Slot == "upgrade1" || e.Slot == "upgrade2")?.Item;
            }

            // Build tooltip text with comparison
            var tooltipText = BuildItemTooltip(item, equippedForComparison);
            row.TooltipText = tooltipText;

            // Color bar
            var colorBar = new ColorRect
            {
                Color = GetRarityColor(item.Rarity),
                CustomMinimumSize = new Vector2(4, 0)
            };
            row.AddChild(colorBar);

            var spacer = new Control { CustomMinimumSize = new Vector2(8, 0) };
            row.AddChild(spacer);

            // Item name
            var nameLabel = new Label
            {
                Text = item.Name,
                CustomMinimumSize = new Vector2(150, 0)
            };
            nameLabel.AddThemeColorOverride("font_color", GetRarityColor(item.Rarity));
            row.AddChild(nameLabel);

            // Item level
            var ilvlLabel = new Label
            {
                Text = $"iLvl {item.ItemLevel}",
                CustomMinimumSize = new Vector2(60, 0),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            ilvlLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            row.AddChild(ilvlLabel);

            // Equip button
            var equipBtn = new Button { Text = "Equip" };
            var itemId = item.Id;
            var slot = item.TowerItemType == TowerItemType.Weapon ? "weapon" : GetAvailableUpgradeSlot();

            if (slot != null)
            {
                equipBtn.Pressed += () => OnEquipPressed(itemId, slot);
            }
            else
            {
                equipBtn.Disabled = true;
            }
            row.AddChild(equipBtn);

            _inventoryList.AddChild(row);
        }
    }

    private string? GetAvailableUpgradeSlot()
    {
        if (_tower == null) return null;

        var equippedSlots = _tower.EquippedItems?.Select(e => e.Slot).ToHashSet() ?? new HashSet<string>();

        if (!equippedSlots.Contains("upgrade1")) return "upgrade1";
        if (!equippedSlots.Contains("upgrade2")) return "upgrade2";
        return null;
    }

    private Color GetRarityColor(TowerItemRarity rarity) => rarity switch
    {
        TowerItemRarity.Common => new Color(0.8f, 0.8f, 0.8f),
        TowerItemRarity.Magic => new Color(0.4f, 0.6f, 1.0f),
        TowerItemRarity.Rare => new Color(1.0f, 1.0f, 0.4f),
        TowerItemRarity.Legendary => new Color(1.0f, 0.6f, 0.2f),
        _ => new Color(1f, 1f, 1f)
    };

    private string BuildItemTooltip(ServerItem item, ServerItem? equippedItem = null)
    {
        var lines = new List<string>
        {
            item.Name,
            $"Item Level: {item.ItemLevel}",
            $"Type: {item.TowerItemType}",
            $"Rarity: {item.Rarity}",
            ""
        };

        // Parse item stats
        var itemStats = ParseItemStats(item);
        var equippedStats = equippedItem != null ? ParseItemStats(equippedItem) : new Dictionary<string, float>();

        // Display stats with comparison
        lines.Add("--- Stats ---");
        foreach (var stat in itemStats)
        {
            var statLine = $"{stat.Key}: {stat.Value:0.##}";

            if (equippedStats.TryGetValue(stat.Key, out var equippedValue))
            {
                var diff = stat.Value - equippedValue;
                if (Math.Abs(diff) > 0.001f)
                {
                    var sign = diff > 0 ? "+" : "";
                    var indicator = diff > 0 ? " [BETTER]" : " [WORSE]";
                    statLine += $" ({sign}{diff:0.##}{indicator})";
                }
            }
            lines.Add(statLine);
        }

        // Parse and display affixes
        var itemAffixes = ParseItemAffixes(item);
        if (itemAffixes.Count > 0)
        {
            lines.Add("");
            lines.Add("--- Affixes ---");
            foreach (var affix in itemAffixes)
            {
                lines.Add($"  {affix.Key}: +{affix.Value:0.##}");
            }
        }

        // Show comparison section if there's an equipped item
        if (equippedItem != null)
        {
            lines.Add("");
            lines.Add("=== Currently Equipped ===");
            lines.Add(equippedItem.Name);
            lines.Add($"Item Level: {equippedItem.ItemLevel}");

            if (equippedStats.Count > 0)
            {
                foreach (var stat in equippedStats)
                {
                    lines.Add($"{stat.Key}: {stat.Value:0.##}");
                }
            }

            var equippedAffixes = ParseItemAffixes(equippedItem);
            if (equippedAffixes.Count > 0)
            {
                lines.Add("Affixes:");
                foreach (var affix in equippedAffixes)
                {
                    lines.Add($"  {affix.Key}: +{affix.Value:0.##}");
                }
            }
        }

        return string.Join("\n", lines);
    }

    private Dictionary<string, float> ParseItemStats(ServerItem item)
    {
        var stats = new Dictionary<string, float>();
        try
        {
            var json = JsonDocument.Parse(item.BaseStatsJson).RootElement;
            if (json.TryGetProperty("damage", out var dmg) && dmg.TryGetSingle(out var dmgVal))
                stats["Damage"] = dmgVal;
            if (json.TryGetProperty("attackSpeed", out var spd) && spd.TryGetSingle(out var spdVal))
                stats["Attack Speed"] = spdVal;
            if (json.TryGetProperty("range", out var rng) && rng.TryGetSingle(out var rngVal))
                stats["Range"] = rngVal;
            if (json.TryGetProperty("critChance", out var crit) && crit.TryGetSingle(out var critVal))
                stats["Crit Chance"] = critVal;
            if (json.TryGetProperty("critDamage", out var critDmg) && critDmg.TryGetSingle(out var critDmgVal))
                stats["Crit Damage"] = critDmgVal;
        }
        catch { }
        return stats;
    }

    private Dictionary<string, float> ParseItemAffixes(ServerItem item)
    {
        var affixes = new Dictionary<string, float>();
        try
        {
            var json = JsonDocument.Parse(item.AffixesJson).RootElement;
            foreach (var affix in json.EnumerateArray())
            {
                if (affix.TryGetProperty("name", out var name) &&
                    affix.TryGetProperty("value", out var value) &&
                    value.TryGetSingle(out var valueFloat))
                {
                    affixes[name.GetString() ?? "Unknown"] = valueFloat;
                }
            }
        }
        catch { }
        return affixes;
    }

    private async void OnEquipPressed(Guid itemId, string slot)
    {
        if (_tower == null || GameManager.Instance?.Api == null) return;

        var success = await GameManager.Instance.Api.EquipItemAsync(_tower.Id, itemId, slot);
        if (success)
        {
            LoadData();
        }
        else
        {
            GD.PrintErr("Failed to equip item");
        }
    }

    private async void OnUnequipPressed(string slot)
    {
        if (_tower == null || GameManager.Instance?.Api == null) return;

        var success = await GameManager.Instance.Api.UnequipItemAsync(_tower.Id, slot);
        if (success)
        {
            LoadData();
        }
        else
        {
            GD.PrintErr("Failed to unequip item");
        }
    }

    private void OnBackPressed()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SelectedTower = null;
        }
        GetTree().ChangeSceneToFile("res://scenes/dashboard.tscn");
    }
}
