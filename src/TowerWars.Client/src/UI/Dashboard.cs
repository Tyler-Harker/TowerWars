using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using TowerWars.Client.Autoloads;
using TowerWars.Client.Data;
using TowerWars.Client.Services;
using TowerWars.Shared.Constants;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.UI;

public partial class Dashboard : Control
{
    private Label? _welcomeLabel;
    private VBoxContainer? _towersContainer;
    private Button? _startGameButton;
    private Button? _logoutButton;
    private Label? _goldLabel;
    private Label? _loadingLabel;

    private PlayerData? _playerData;
    private List<ServerTower> _serverTowers = new();

    // Equipment management state
    private List<ServerItem> _playerItems = new();
    private ServerTower? _selectedTower;
    private PanelContainer? _equipmentPanel;
    private bool _isLoadingTowers = false;

    public override void _Ready()
    {
        _welcomeLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/Header/WelcomeLabel");
        _towersContainer = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/ContentArea/TowersPanel/VBoxContainer/TowersList");
        _startGameButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/ContentArea/ActionsPanel/VBoxContainer/StartGameButton");
        _logoutButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/Header/LogoutButton");
        _goldLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/Header/GoldLabel");

        _startGameButton?.Connect("pressed", Callable.From(OnStartGamePressed));
        _logoutButton?.Connect("pressed", Callable.From(OnLogoutPressed));

        LoadPlayerData();

        // Subscribe to player data events from ENet
        if (GameManager.Instance?.PacketHandler != null)
        {
            GameManager.Instance.PacketHandler.PlayerTowersReceived += OnPlayerTowersReceived;
            GameManager.Instance.PacketHandler.PlayerItemsReceived += OnPlayerItemsReceived;
        }

        // Connect to ZoneServer and request data via UDP
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AutoSceneSwitch = false;
            GameManager.Instance.Network.Connected += OnNetworkConnected;
            GameManager.Instance.Network.Disconnected += OnNetworkDisconnected;

            if (GameManager.Instance.Network.IsConnected)
            {
                RequestPlayerData();
            }
            else
            {
                _isLoadingTowers = true;
                UpdateTowersListLoading();
                GameManager.Instance.ConnectToServer();
            }
        }

        UpdateUI();
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AutoSceneSwitch = true;
            GameManager.Instance.Network.Connected -= OnNetworkConnected;
            GameManager.Instance.Network.Disconnected -= OnNetworkDisconnected;
        }
        if (GameManager.Instance?.PacketHandler != null)
        {
            GameManager.Instance.PacketHandler.PlayerTowersReceived -= OnPlayerTowersReceived;
            GameManager.Instance.PacketHandler.PlayerItemsReceived -= OnPlayerItemsReceived;
        }
    }

    private void OnNetworkConnected()
    {
        GD.Print("Connected to ZoneServer from Dashboard, requesting player data...");
        RequestPlayerData();
    }

    private void OnNetworkDisconnected(string reason)
    {
        _isLoadingTowers = false;
        GD.PrintErr($"ZoneServer connection lost: {reason}");
    }

    private void RequestPlayerData()
    {
        _isLoadingTowers = true;
        UpdateTowersListLoading();
        GameManager.Instance?.Network.SendPlayerDataRequest();
    }

    private void OnPlayerTowersReceived(PlayerTowersResponsePacket response)
    {
        if (!response.Success)
        {
            GD.PrintErr($"Failed to get towers: {response.ErrorMessage}");
            _isLoadingTowers = false;
            UpdateTowersList();
            return;
        }

        _serverTowers = response.Towers.Select(MapFromWire).ToList();
        SyncLocalDataWithServer();
        _isLoadingTowers = false;
        UpdateTowersList();
    }

    private void OnPlayerItemsReceived(PlayerItemsResponsePacket response)
    {
        if (!response.Success)
        {
            GD.PrintErr($"Failed to get items: {response.ErrorMessage}");
            return;
        }

        _playerItems = response.Items.Select(MapFromWire).ToList();
        UpdateTowersList();
    }

    private static ServerTower MapFromWire(WirePlayerTower w) => new()
    {
        Id = w.Id,
        UserId = w.UserId,
        Name = w.Name,
        WeaponType = (WeaponType)w.WeaponType,
        DamageType = (DamageType)w.DamageType,
        Level = w.Level,
        Experience = w.Experience,
        SkillPoints = w.SkillPoints,
        BaseDamage = w.BaseDamage,
        BaseAttackSpeed = w.BaseAttackSpeed,
        BaseRange = w.BaseRange,
        BaseCritChance = w.BaseCritChance,
        BaseCritDamage = w.BaseCritDamage,
        SkillAllocations = w.SkillAllocations.Select(s => new ServerSkillAllocation
        {
            Id = s.Id, TowerId = s.TowerId, SkillId = s.SkillId, Points = s.Points
        }).ToList(),
        EquippedItems = w.EquippedItems.Select(e => new ServerEquippedItem
        {
            Id = e.Id, TowerId = e.TowerId, ItemId = e.ItemId, Slot = e.Slot,
            Item = e.Item != null ? MapFromWire(e.Item) : null
        }).ToList()
    };

    private static ServerItem MapFromWire(WirePlayerItem w) => new()
    {
        Id = w.Id,
        UserId = w.UserId,
        Name = w.Name,
        TowerItemType = (TowerItemType)w.ItemType,
        Rarity = (TowerItemRarity)w.Rarity,
        ItemLevel = w.ItemLevel,
        BaseStatsJson = w.BaseStatsJson,
        AffixesJson = w.AffixesJson,
        IsEquipped = w.IsEquipped,
        DroppedAt = DateTimeOffset.FromUnixTimeMilliseconds(w.DroppedAtUnixMs).UtcDateTime,
        CollectedAt = w.CollectedAtUnixMs.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(w.CollectedAtUnixMs.Value).UtcDateTime
            : null
    };

    private void LoadPlayerData()
    {
        _playerData = PlayerData.Load();
    }

    private void SyncLocalDataWithServer()
    {
        if (_playerData == null) return;

        _playerData.Towers.Clear();
        foreach (var serverTower in _serverTowers)
        {
            _playerData.Towers.Add(new TowerData
            {
                Id = serverTower.Id.ToString(),
                Name = serverTower.Name,
                WeaponType = serverTower.WeaponType,
                DamageType = serverTower.DamageType,
                WeaponLevel = serverTower.Level,
                Experience = serverTower.Experience,
                SkillPoints = serverTower.SkillPoints
            });
        }
        _playerData.Save();
    }

    private Color GetDamageTypeDisplayColor(DamageType damageType)
    {
        return damageType switch
        {
            DamageType.Physical => new Color(0.8f, 0.7f, 0.6f),
            DamageType.Cold => new Color(0.5f, 0.85f, 1.0f),
            DamageType.Lightning => new Color(1.0f, 1.0f, 0.4f),
            DamageType.Fire => new Color(1.0f, 0.5f, 0.2f),
            DamageType.Chaos => new Color(0.7f, 0.3f, 0.9f),
            DamageType.Holy => new Color(1.0f, 0.95f, 0.75f),
            _ => new Color(0.7f, 0.7f, 0.7f)
        };
    }

    private void UpdateUI()
    {
        if (_welcomeLabel != null)
        {
            var username = GameManager.Instance?.Username ?? "Commander";
            _welcomeLabel.Text = $"Welcome, {username}!";
        }

        if (_goldLabel != null && _playerData != null)
        {
            _goldLabel.Text = $"Gold: {_playerData.Gold}";
        }

        UpdateTowersList();
    }

    private void UpdateTowersListLoading()
    {
        if (_towersContainer == null) return;

        foreach (var child in _towersContainer.GetChildren())
        {
            child.QueueFree();
        }

        var loadingLabel = new Label
        {
            Text = "Loading towers from server...",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _towersContainer.AddChild(loadingLabel);
    }

    private void UpdateTowersList()
    {
        if (_towersContainer == null || _playerData == null) return;

        // Clear existing tower items
        foreach (var child in _towersContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Add tower cards - use server data if available
        if (_serverTowers.Count > 0)
        {
            foreach (var tower in _serverTowers)
            {
                var towerCard = CreateServerTowerCard(tower);
                _towersContainer.AddChild(towerCard);
            }
        }
        else
        {
            foreach (var tower in _playerData.Towers)
            {
                var towerCard = CreateTowerCard(tower);
                _towersContainer.AddChild(towerCard);
            }
        }

        if (_playerData.Towers.Count == 0 && _serverTowers.Count == 0 && !_isLoadingTowers)
        {
            var emptyLabel = new Label
            {
                Text = "No towers yet!",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _towersContainer.AddChild(emptyLabel);
        }
    }

    private Control CreateServerTowerCard(ServerTower tower)
    {
        // Use PanelContainer as the root with a button overlay for clicks
        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(0, 110);
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.SizeFlagsVertical = SizeFlags.ExpandFill;
        card.AddChild(margin);

        // Add clickable button overlay
        var button = new Button();
        button.Flat = true;
        button.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        button.Pressed += () => OnTowerSelected(tower);
        card.AddChild(button);

        var hbox = new HBoxContainer();
        margin.AddChild(hbox);

        // Tower icon placeholder with level badge
        var iconContainer = new Control();
        iconContainer.CustomMinimumSize = new Vector2(70, 70);
        hbox.AddChild(iconContainer);

        var iconPanel = new Panel();
        iconPanel.CustomMinimumSize = new Vector2(60, 60);
        iconContainer.AddChild(iconPanel);

        var levelBadge = new Label
        {
            Text = tower.Level.ToString(),
            Position = new Vector2(45, 45),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        levelBadge.AddThemeFontSizeOverride("font_size", 14);
        levelBadge.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0.2f));
        iconContainer.AddChild(levelBadge);

        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(10, 0);
        hbox.AddChild(spacer);

        // Tower info
        var infoVBox = new VBoxContainer();
        infoVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(infoVBox);

        var nameLabel = new Label { Text = tower.Name };
        nameLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        infoVBox.AddChild(nameLabel);

        var weaponLabel = new Label { Text = $"{tower.DamageType} {tower.WeaponType} - Level {tower.Level}" };
        weaponLabel.AddThemeColorOverride("font_color", GetDamageTypeDisplayColor(tower.DamageType));
        infoVBox.AddChild(weaponLabel);

        // Experience bar
        var expContainer = new HBoxContainer();
        infoVBox.AddChild(expContainer);

        var expLabel = new Label { Text = "XP: " };
        expLabel.AddThemeFontSizeOverride("font_size", 12);
        expLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        expContainer.AddChild(expLabel);

        var expProgress = new ProgressBar();
        expProgress.CustomMinimumSize = new Vector2(150, 16);
        expProgress.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Calculate experience progress
        var nextLevelExp = GetNextLevelExperience(tower.Level);
        var currentLevelExp = GetCurrentLevelExperience(tower.Level);
        var progressValue = nextLevelExp > currentLevelExp
            ? (float)(tower.Experience - currentLevelExp) / (nextLevelExp - currentLevelExp) * 100
            : 100;
        expProgress.Value = progressValue;
        expContainer.AddChild(expProgress);

        var expValueLabel = new Label { Text = $"{tower.Experience}/{nextLevelExp}" };
        expValueLabel.AddThemeFontSizeOverride("font_size", 12);
        expValueLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        expContainer.AddChild(expValueLabel);

        // Skill points indicator
        if (tower.SkillPoints > 0)
        {
            var skillPointsLabel = new Label
            {
                Text = $"Skill Points Available: {tower.SkillPoints}",
                Modulate = new Color(0.2f, 1f, 0.2f)
            };
            skillPointsLabel.AddThemeFontSizeOverride("font_size", 12);
            infoVBox.AddChild(skillPointsLabel);
        }

        // Equipment indicator
        var equippedCount = tower.EquippedItems?.Count ?? 0;
        if (equippedCount > 0)
        {
            var equipLabel = new Label
            {
                Text = $"Equipment: {equippedCount}/3 slots"
            };
            equipLabel.AddThemeFontSizeOverride("font_size", 11);
            equipLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 1.0f));
            infoVBox.AddChild(equipLabel);
        }

        return card;
    }

    private long GetCurrentLevelExperience(int level)
    {
        long[] thresholds = { 0, 100, 300, 600, 1000, 1500, 2200, 3100, 4200, 5500,
            7000, 8800, 10900, 13300, 16100, 19400, 23200, 27600, 32700, 38500 };
        return level > 0 && level <= thresholds.Length ? thresholds[level - 1] : 0;
    }

    private long GetNextLevelExperience(int level)
    {
        long[] thresholds = { 0, 100, 300, 600, 1000, 1500, 2200, 3100, 4200, 5500,
            7000, 8800, 10900, 13300, 16100, 19400, 23200, 27600, 32700, 38500 };
        return level >= 0 && level < thresholds.Length ? thresholds[level] : 38500;
    }

    private Control CreateTowerCard(TowerData tower)
    {
        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(0, 80);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        card.AddChild(margin);

        var hbox = new HBoxContainer();
        margin.AddChild(hbox);

        // Tower icon placeholder
        var iconPanel = new Panel();
        iconPanel.CustomMinimumSize = new Vector2(60, 60);
        hbox.AddChild(iconPanel);

        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(10, 0);
        hbox.AddChild(spacer);

        // Tower info
        var infoVBox = new VBoxContainer();
        infoVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(infoVBox);

        var nameLabel = new Label { Text = tower.Name };
        nameLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        infoVBox.AddChild(nameLabel);

        var weaponLabel = new Label { Text = $"{tower.WeaponType} - Level {tower.WeaponLevel}" };
        weaponLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        infoVBox.AddChild(weaponLabel);

        return card;
    }

    private void OnStartGamePressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/mode_selection.tscn");
    }

    private void OnLogoutPressed()
    {
        // Clear auth state
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AuthToken = null;
            GameManager.Instance.Disconnect();
        }
        GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
    }

    private void OnTowerSelected(ServerTower tower)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SelectedTower = tower;
        }
        GetTree().ChangeSceneToFile("res://scenes/tower_detail.tscn");
    }

    private void ShowEquipmentPanel()
    {
        if (_selectedTower == null) return;

        // Remove existing panel if any
        _equipmentPanel?.QueueFree();

        _equipmentPanel = new PanelContainer();
        _equipmentPanel.SetAnchorsPreset(LayoutPreset.Center);
        _equipmentPanel.CustomMinimumSize = new Vector2(500, 500);
        _equipmentPanel.Position = new Vector2(-250, -250);
        AddChild(_equipmentPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 15);
        margin.AddThemeConstantOverride("margin_right", 15);
        margin.AddThemeConstantOverride("margin_top", 15);
        margin.AddThemeConstantOverride("margin_bottom", 15);
        _equipmentPanel.AddChild(margin);

        var mainVBox = new VBoxContainer();
        margin.AddChild(mainVBox);

        // Header with tower name and close button
        var header = new HBoxContainer();
        mainVBox.AddChild(header);

        var titleLabel = new Label
        {
            Text = $"Equipment - {_selectedTower.Name}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(titleLabel);

        var closeButton = new Button { Text = "X" };
        closeButton.Pressed += CloseEquipmentPanel;
        header.AddChild(closeButton);

        // Separator
        mainVBox.AddChild(new HSeparator());

        // Equipment slots section
        var slotsLabel = new Label { Text = "Equipment Slots" };
        slotsLabel.AddThemeFontSizeOverride("font_size", 14);
        mainVBox.AddChild(slotsLabel);

        var slotsContainer = new VBoxContainer();
        mainVBox.AddChild(slotsContainer);

        string[] slots = { "weapon", "upgrade1", "upgrade2" };
        string[] slotNames = { "Weapon", "Upgrade 1", "Upgrade 2" };

        for (int i = 0; i < slots.Length; i++)
        {
            var slotRow = CreateEquipmentSlotRow(slots[i], slotNames[i]);
            slotsContainer.AddChild(slotRow);
        }

        // Separator
        mainVBox.AddChild(new HSeparator());

        // Inventory section
        var inventoryLabel = new Label { Text = "Inventory (Unequipped Items)" };
        inventoryLabel.AddThemeFontSizeOverride("font_size", 14);
        mainVBox.AddChild(inventoryLabel);

        var scrollContainer = new ScrollContainer();
        scrollContainer.CustomMinimumSize = new Vector2(0, 200);
        scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainVBox.AddChild(scrollContainer);

        var inventoryVBox = new VBoxContainer();
        inventoryVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scrollContainer.AddChild(inventoryVBox);

        // Filter items that aren't equipped
        var unequippedItems = _playerItems.Where(item => !item.IsEquipped).ToList();

        if (unequippedItems.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "No items in inventory",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            inventoryVBox.AddChild(emptyLabel);
        }
        else
        {
            foreach (var item in unequippedItems)
            {
                var itemRow = CreateInventoryItemRow(item);
                inventoryVBox.AddChild(itemRow);
            }
        }
    }

    private Control CreateEquipmentSlotRow(string slot, string slotDisplayName)
    {
        var row = new HBoxContainer();
        row.CustomMinimumSize = new Vector2(0, 40);

        var slotLabel = new Label
        {
            Text = $"{slotDisplayName}:",
            CustomMinimumSize = new Vector2(80, 0)
        };
        row.AddChild(slotLabel);

        // Find equipped item in this slot
        var equippedItemLink = _selectedTower?.EquippedItems?.FirstOrDefault(e => e.Slot == slot);
        ServerItem? equippedItem = null;

        if (equippedItemLink != null)
        {
            equippedItem = _playerItems.FirstOrDefault(i => i.Id == equippedItemLink.ItemId);
        }

        if (equippedItem != null)
        {
            // Show equipped item with rarity color
            var itemColor = GetRarityColor(equippedItem.Rarity);
            var itemLabel = new Label
            {
                Text = $"{equippedItem.Name} (iLvl {equippedItem.ItemLevel})",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            itemLabel.AddThemeColorOverride("font_color", itemColor);
            row.AddChild(itemLabel);

            // Unequip button
            var unequipButton = new Button { Text = "Unequip" };
            unequipButton.Pressed += () => OnUnequipPressed(slot);
            row.AddChild(unequipButton);
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

        return row;
    }

    private Control CreateInventoryItemRow(ServerItem item)
    {
        var row = new HBoxContainer();
        row.CustomMinimumSize = new Vector2(0, 35);

        // Rarity color bar
        var colorBar = new ColorRect();
        colorBar.Color = GetRarityColor(item.Rarity);
        colorBar.CustomMinimumSize = new Vector2(4, 0);
        row.AddChild(colorBar);

        var spacer = new Control { CustomMinimumSize = new Vector2(8, 0) };
        row.AddChild(spacer);

        // Item name with rarity color
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
            CustomMinimumSize = new Vector2(60, 0)
        };
        ilvlLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        row.AddChild(ilvlLabel);

        // Item type
        var typeLabel = new Label
        {
            Text = item.TowerItemType.ToString(),
            CustomMinimumSize = new Vector2(80, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        typeLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        row.AddChild(typeLabel);

        // Equip button - determine which slot based on item type
        var equipButton = new Button { Text = "Equip" };

        if (item.TowerItemType == TowerItemType.Weapon)
        {
            equipButton.Pressed += () => OnEquipPressed(item.Id, "weapon");
        }
        else if (item.TowerItemType == TowerItemType.TowerUpgrade)
        {
            // Find first available upgrade slot
            var slot = GetAvailableUpgradeSlot();
            if (slot != null)
            {
                equipButton.Pressed += () => OnEquipPressed(item.Id, slot);
            }
            else
            {
                equipButton.Disabled = true;
                equipButton.TooltipText = "No upgrade slots available";
            }
        }
        else
        {
            equipButton.Disabled = true;
            equipButton.TooltipText = "This item type cannot be equipped";
        }

        row.AddChild(equipButton);

        return row;
    }

    private string? GetAvailableUpgradeSlot()
    {
        if (_selectedTower == null) return null;

        var equippedSlots = _selectedTower.EquippedItems?.Select(e => e.Slot).ToHashSet() ?? new HashSet<string>();

        if (!equippedSlots.Contains("upgrade1")) return "upgrade1";
        if (!equippedSlots.Contains("upgrade2")) return "upgrade2";
        return null;
    }

    private Color GetRarityColor(TowerItemRarity rarity)
    {
        return rarity switch
        {
            TowerItemRarity.Common => new Color(0.8f, 0.8f, 0.8f),
            TowerItemRarity.Magic => new Color(0.4f, 0.6f, 1.0f),
            TowerItemRarity.Rare => new Color(1.0f, 1.0f, 0.4f),
            TowerItemRarity.Legendary => new Color(1.0f, 0.6f, 0.2f),
            _ => new Color(1f, 1f, 1f)
        };
    }

    private async void OnEquipPressed(Guid itemId, string slot)
    {
        if (_selectedTower == null || GameManager.Instance?.Api == null) return;

        var success = await GameManager.Instance.Api.EquipItemAsync(_selectedTower.Id, itemId, slot);
        if (success)
        {
            // Re-request data from server via UDP
            RequestPlayerData();
        }
        else
        {
            GD.PrintErr("Failed to equip item");
        }
    }

    private async void OnUnequipPressed(string slot)
    {
        if (_selectedTower == null || GameManager.Instance?.Api == null) return;

        var success = await GameManager.Instance.Api.UnequipItemAsync(_selectedTower.Id, slot);
        if (success)
        {
            // Re-request data from server via UDP
            RequestPlayerData();
        }
        else
        {
            GD.PrintErr("Failed to unequip item");
        }
    }

    private void CloseEquipmentPanel()
    {
        _equipmentPanel?.QueueFree();
        _equipmentPanel = null;
        _selectedTower = null;
    }
}
