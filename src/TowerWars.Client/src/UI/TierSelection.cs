using Godot;
using TowerWars.Client.Autoloads;
using TowerWars.Client.Data;

namespace TowerWars.Client.UI;

public partial class TierSelection : Control
{
    private GridContainer? _tiersGrid;
    private Button? _backButton;
    private Label? _selectedTierLabel;
    private Button? _playButton;

    private PlayerData? _playerData;
    private int _selectedTier = -1;

    public override void _Ready()
    {
        _tiersGrid = GetNodeOrNull<GridContainer>("MarginContainer/VBoxContainer/ContentArea/TiersPanel/ScrollContainer/TiersGrid");
        _backButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/Header/BackButton");
        _selectedTierLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/ContentArea/InfoPanel/VBoxContainer/SelectedTierLabel");
        _playButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/ContentArea/InfoPanel/VBoxContainer/PlayButton");

        _backButton?.Connect("pressed", Callable.From(OnBackPressed));
        _playButton?.Connect("pressed", Callable.From(OnPlayPressed));

        if (_playButton != null)
            _playButton.Disabled = true;

        LoadPlayerData();
        PopulateTiers();
    }

    private void LoadPlayerData()
    {
        _playerData = PlayerData.Load();
    }

    private void PopulateTiers()
    {
        if (_tiersGrid == null || _playerData == null) return;

        // Clear existing
        foreach (var child in _tiersGrid.GetChildren())
        {
            child.QueueFree();
        }

        // Add tier buttons
        foreach (var tier in TierInfo.Tiers)
        {
            var tierButton = CreateTierButton(tier, tier.Tier <= _playerData.HighestTierUnlocked);
            _tiersGrid.AddChild(tierButton);
        }
    }

    private Button CreateTierButton(TierData tier, bool unlocked)
    {
        var button = new Button();
        button.CustomMinimumSize = new Vector2(150, 100);
        button.Text = unlocked ? $"Tier {tier.Tier}\n{tier.Name}" : $"Tier {tier.Tier}\nLocked";
        button.Disabled = !unlocked;

        if (unlocked)
        {
            button.Pressed += () => OnTierSelected(tier);
        }

        // Visual feedback for locked tiers
        if (!unlocked)
        {
            button.Modulate = new Color(0.5f, 0.5f, 0.5f);
        }

        return button;
    }

    private void OnTierSelected(TierData tier)
    {
        _selectedTier = tier.Tier;

        if (_selectedTierLabel != null)
        {
            _selectedTierLabel.Text = $"Tier {tier.Tier}: {tier.Name}\n\n{tier.Description}\n\nWaves: {(tier.WaveCount > 0 ? tier.WaveCount.ToString() : "Endless")}";
        }

        if (_playButton != null)
        {
            _playButton.Disabled = false;
            _playButton.Text = $"Play Tier {tier.Tier}";
        }
    }

    private void OnPlayPressed()
    {
        if (_selectedTier <= 0) return;

        GD.Print($"Starting Tier {_selectedTier}");

        // Store selected tier in GameManager for the game to use
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SelectedTier = _selectedTier;
        }

        // Load the server-authoritative solo game scene
        GetTree().ChangeSceneToFile("res://scenes/server_solo_game.tscn");
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/mode_selection.tscn");
    }
}
