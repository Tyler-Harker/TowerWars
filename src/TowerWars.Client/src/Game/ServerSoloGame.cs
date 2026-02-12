using System.Collections.Generic;
using System.Linq;
using Godot;
using TowerWars.Client.Autoloads;
using TowerWars.Client.Data;
using TowerWars.Client.Networking;
using TowerWars.Client.Services;
using TowerWars.Shared.Constants;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.Game;

/// <summary>
/// Server-authoritative solo game mode. This is a thin wrapper that:
/// - Connects to the ZoneServer
/// - Uses GameWorld for entity rendering
/// - Provides UI for tower selection
/// - Subscribes to PacketHandler signals for state updates
///
/// All game logic (waves, enemies, damage, items) runs on the server.
/// </summary>
public partial class ServerSoloGame : Node2D
{
    private GameWorld _gameWorld = null!;
    private NetworkManager? _network;
    private PacketHandler? _packetHandler;

    // UI elements
    private Label? _waveLabel;
    private Label? _livesLabel;
    private Label? _goldLabel;
    private Label? _statusLabel;
    private Button? _backButton;
    private HBoxContainer? _towerButtons;
    private VBoxContainer? _connectingUI;

    // Game state (synced from server)
    private int _currentWave;
    private int _lives = GameConstants.StartingLives;
    private int _gold = GameConstants.StartingGold;
    private int _score;
    private int _currentTier = 1;
    private bool _isConnected;
    private bool _matchStarted;
    private bool _isPaused;

    // Pause UI
    private PanelContainer? _pauseOverlay;
    private AcceptDialog? _endGamePopup;

    // Player towers (from server)
    private List<ServerTower> _playerTowers = new();

    // Tower placement
    private TowerType? _selectedTowerType;
    private Guid _selectedPlayerTowerId;
    private Label? _selectedTowerLabel;
    private Sprite2D? _towerPreview;

    public override void _Ready()
    {
        _currentTier = GameManager.Instance?.SelectedTier ?? 1;

        SetupGameWorld();
        SetupUI();
        SetupNetworking();

        // Start connection to server
        ConnectToServer();
    }

    public override void _ExitTree()
    {
        UnsubscribeFromEvents();
    }

    private void SetupGameWorld()
    {
        _gameWorld = new GameWorld();
        _gameWorld.Name = "GameWorld";
        AddChild(_gameWorld);

        // Tower placement preview (semi-transparent ghost)
        _towerPreview = new Sprite2D();
        _towerPreview.Texture = GD.Load<Texture2D>("res://icon.svg");
        _towerPreview.Scale = new Vector2(0.5f, 0.5f);
        _towerPreview.Modulate = new Color(0.3f, 1.0f, 0.3f, 0.5f);
        _towerPreview.Visible = false;
        _towerPreview.ZIndex = 10;
        _gameWorld.AddChild(_towerPreview);
    }

    private void SetupUI()
    {
        var uiLayer = new CanvasLayer();
        AddChild(uiLayer);

        var marginContainer = new MarginContainer();
        marginContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        marginContainer.AddThemeConstantOverride("margin_left", 20);
        marginContainer.AddThemeConstantOverride("margin_right", 20);
        marginContainer.AddThemeConstantOverride("margin_top", 20);
        marginContainer.AddThemeConstantOverride("margin_bottom", 20);
        uiLayer.AddChild(marginContainer);

        var vbox = new VBoxContainer();
        marginContainer.AddChild(vbox);

        // Top bar
        var topBar = new HBoxContainer();
        vbox.AddChild(topBar);

        _backButton = new Button { Text = "Back" };
        _backButton.Pressed += OnBackPressed;
        topBar.AddChild(_backButton);

        var tierLabel = new Label { Text = $"Tier {_currentTier}: {TierInfo.Tiers[_currentTier - 1].Name}" };
        tierLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        tierLabel.HorizontalAlignment = HorizontalAlignment.Center;
        topBar.AddChild(tierLabel);

        _waveLabel = new Label { Text = "Wave: 0" };
        topBar.AddChild(_waveLabel);

        var spacer1 = new Control { CustomMinimumSize = new Vector2(20, 0) };
        topBar.AddChild(spacer1);

        _livesLabel = new Label { Text = $"Lives: {_lives}" };
        topBar.AddChild(_livesLabel);

        var spacer2 = new Control { CustomMinimumSize = new Vector2(20, 0) };
        topBar.AddChild(spacer2);

        _goldLabel = new Label { Text = $"Gold: {_gold}" };
        topBar.AddChild(_goldLabel);

        // Connecting UI (centered)
        _connectingUI = new VBoxContainer();
        _connectingUI.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _connectingUI.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(_connectingUI);

        _statusLabel = new Label
        {
            Text = "Connecting to server...",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 24);
        _connectingUI.AddChild(_statusLabel);

        // Spacer to push bottom panel down
        var middleSpacer = new Control();
        middleSpacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(middleSpacer);

        // Bottom panel (tower selection)
        var bottomPanel = new PanelContainer();
        bottomPanel.Visible = false;
        vbox.AddChild(bottomPanel);

        var bottomHBox = new HBoxContainer();
        bottomPanel.AddChild(bottomHBox);

        _towerButtons = new HBoxContainer();
        bottomHBox.AddChild(_towerButtons);

        // Tower buttons are populated dynamically when player towers are received from server

        // Selected tower indicator
        var spacer3 = new Control { CustomMinimumSize = new Vector2(20, 0) };
        bottomHBox.AddChild(spacer3);

        _selectedTowerLabel = new Label
        {
            Text = "Click tower to select, then click on grid to place",
            VerticalAlignment = VerticalAlignment.Center
        };
        bottomHBox.AddChild(_selectedTowerLabel);
    }

    private void SetupNetworking()
    {
        _network = GameManager.Instance?.Network;
        _packetHandler = GameManager.Instance?.PacketHandler;

        if (_network == null || _packetHandler == null)
        {
            GD.PrintErr("NetworkManager or PacketHandler not found!");
            return;
        }

        // Subscribe to network events
        _network.Connected += OnConnected;
        _network.Disconnected += OnDisconnected;

        // Subscribe to game events
        _packetHandler.MatchStarted += OnMatchStarted;
        _packetHandler.MatchEnded += OnMatchEnded;
        _packetHandler.WaveStarted += OnWaveStarted;
        _packetHandler.WaveEnded += OnWaveEnded;
        _packetHandler.PlayerStateUpdated += OnPlayerStateUpdated;
        _packetHandler.ErrorReceived += OnErrorReceived;
        _packetHandler.GamePaused += OnGamePaused;
        _packetHandler.RequestMatchAckReceived += OnRequestMatchAck;
        _packetHandler.ReturnedToLobby += OnReturnedToLobby;
        _packetHandler.PlayerTowersReceived += OnPlayerTowersReceived;
    }

    private void UnsubscribeFromEvents()
    {
        if (_network != null)
        {
            _network.Connected -= OnConnected;
            _network.Disconnected -= OnDisconnected;
        }

        if (_packetHandler != null)
        {
            _packetHandler.MatchStarted -= OnMatchStarted;
            _packetHandler.MatchEnded -= OnMatchEnded;
            _packetHandler.WaveStarted -= OnWaveStarted;
            _packetHandler.WaveEnded -= OnWaveEnded;
            _packetHandler.PlayerStateUpdated -= OnPlayerStateUpdated;
            _packetHandler.ErrorReceived -= OnErrorReceived;
            _packetHandler.GamePaused -= OnGamePaused;
            _packetHandler.RequestMatchAckReceived -= OnRequestMatchAck;
            _packetHandler.ReturnedToLobby -= OnReturnedToLobby;
            _packetHandler.PlayerTowersReceived -= OnPlayerTowersReceived;
        }
    }

    private void ConnectToServer()
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            UpdateStatus("Error: GameManager not found");
            return;
        }

        // Disable auto scene switch - we manage our own scene lifecycle
        gameManager.AutoSceneSwitch = false;

        // If already connected (e.g. Dashboard left connection open), proceed directly
        if (gameManager.Network.IsConnected)
        {
            OnConnected();
            return;
        }

        UpdateStatus("Connecting to server...");
        gameManager.ConnectToServer();
    }

    private void OnConnected()
    {
        _isConnected = true;
        UpdateStatus("Connected! Loading towers...");

        GD.Print($"[ServerSoloGame] OnConnected fired, requesting player data...");

        // Request player towers before starting match
        _network?.SendPlayerDataRequest();
    }

    private void OnRequestMatchAck(RequestMatchAckPacket packet)
    {
        if (packet.Success)
        {
            GD.Print($"[ServerSoloGame] Match created: {packet.MatchId}");
            UpdateStatus("Match found! Waiting for start...");

            // Send ready signal now that we're in a session
            _network?.SendReady(true);
        }
        else
        {
            GD.PrintErr($"[ServerSoloGame] Match request failed: {packet.ErrorMessage}");
            UpdateStatus($"Failed to start match: {packet.ErrorMessage}");
        }
    }

    private void OnReturnedToLobby()
    {
        GD.Print("[ServerSoloGame] Returned to lobby");
        _matchStarted = false;
    }

    private void OnPlayerTowersReceived(PlayerTowersResponsePacket response)
    {
        if (!response.Success)
        {
            GD.PrintErr($"[ServerSoloGame] Failed to get towers: {response.ErrorMessage}");
            UpdateStatus("Failed to load towers");
            return;
        }

        _playerTowers = response.Towers.Select(w => new ServerTower
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
            BaseCritDamage = w.BaseCritDamage
        }).ToList();

        GD.Print($"[ServerSoloGame] Received {_playerTowers.Count} towers");
        PopulateTowerButtons();

        // Now request the match
        UpdateStatus("Towers loaded! Requesting match...");
        _network?.SendRequestMatch(GameMode.Solo, (byte)_currentTier);
    }

    private void PopulateTowerButtons()
    {
        if (_towerButtons == null) return;

        // Clear existing buttons
        foreach (var child in _towerButtons.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var tower in _playerTowers)
        {
            var towerType = MapWeaponToTowerType(tower.WeaponType);
            var stats = TowerDefinitions.GetStats(towerType);
            var towerBtn = new Button
            {
                Text = $"{tower.Name}\n{stats.Cost}g",
                CustomMinimumSize = new Vector2(100, 60)
            };
            var capturedTower = tower;
            var capturedType = towerType;
            towerBtn.Pressed += () => OnTowerButtonPressed(capturedType, capturedTower.Id, capturedTower.Name);
            _towerButtons.AddChild(towerBtn);
        }
    }

    private static TowerType MapWeaponToTowerType(WeaponType weaponType) => weaponType switch
    {
        WeaponType.Bow => TowerType.Archer,
        WeaponType.Cannon => TowerType.Cannon,
        WeaponType.Club => TowerType.Basic,
        WeaponType.Axe => TowerType.Basic,
        WeaponType.Sword => TowerType.Basic,
        _ => TowerType.Basic
    };

    private void OnDisconnected(string reason)
    {
        _isConnected = false;
        UpdateStatus($"Disconnected: {reason}");
    }

    private void OnMatchStarted(string matchId)
    {
        _matchStarted = true;
        UpdateStatus("Match started!");

        // Hide connecting UI, show game UI
        if (_connectingUI != null)
            _connectingUI.Visible = false;

        // Show tower selection panel
        var bottomPanel = _towerButtons?.GetParent()?.GetParent();
        if (bottomPanel is Control control)
            control.Visible = true;
    }

    private void OnMatchEnded(string result)
    {
        _matchStarted = false;

        var isVictory = result == "Victory";
        if (isVictory)
        {
            ShowVictoryScreen();
        }
        else
        {
            ShowGameOverScreen();
        }
    }

    private void OnWaveStarted(int waveNumber)
    {
        _currentWave = waveNumber;
        UpdateUI();
    }

    private void OnWaveEnded(int waveNumber, bool success, int bonusGold)
    {
        if (success)
        {
            GD.Print($"Wave {waveNumber} complete! Bonus: {bonusGold} gold");
        }
    }

    private void OnPlayerStateUpdated(uint playerId, int gold, int lives, int score)
    {
        if (_network != null && playerId == _network.PlayerId)
        {
            _gold = gold;
            _lives = lives;
            _score = score;
            UpdateUI();

            if (_lives <= 0)
            {
                // Server will send MatchEnded, but we can show feedback immediately
                UpdateStatus("Defeat!");
            }
        }
    }

    private void OnErrorReceived(int code, string message)
    {
        GD.PrintErr($"Server error [{code}]: {message}");

        // Show error to user if it's a placement error
        if (code == (int)ErrorCode.InsufficientGold)
        {
            ShowNotEnoughGold();
        }
        else if (code == (int)ErrorCode.InvalidPlacement)
        {
            ShowInvalidPlacement();
        }
    }

    private void OnGamePaused(bool isPaused, string? reason)
    {
        _isPaused = isPaused;

        if (isPaused)
        {
            ShowPauseOverlay(reason);
        }
        else
        {
            HidePauseOverlay();
        }
    }

    private void ShowPauseOverlay(string? reason)
    {
        if (_pauseOverlay != null)
        {
            _pauseOverlay.Visible = true;
            var label = _pauseOverlay.GetNode<Label>("VBox/ReasonLabel");
            if (label != null)
                label.Text = reason ?? "Game Paused";
            return;
        }

        // Create pause overlay
        var overlay = new CanvasLayer();
        overlay.Layer = 100; // Above other UI
        AddChild(overlay);

        _pauseOverlay = new PanelContainer();
        _pauseOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var styleBox = new StyleBoxFlat();
        styleBox.BgColor = new Color(0, 0, 0, 0.7f);
        _pauseOverlay.AddThemeStyleboxOverride("panel", styleBox);
        overlay.AddChild(_pauseOverlay);

        var vbox = new VBoxContainer();
        vbox.Name = "VBox";
        vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
        vbox.GrowHorizontal = Control.GrowDirection.Both;
        vbox.GrowVertical = Control.GrowDirection.Both;
        _pauseOverlay.AddChild(vbox);

        var titleLabel = new Label
        {
            Text = "PAUSED",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 48);
        vbox.AddChild(titleLabel);

        var reasonLabel = new Label
        {
            Name = "ReasonLabel",
            Text = reason ?? "Game Paused",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        reasonLabel.AddThemeFontSizeOverride("font_size", 24);
        vbox.AddChild(reasonLabel);
    }

    private void HidePauseOverlay()
    {
        if (_pauseOverlay != null)
        {
            _pauseOverlay.Visible = false;
        }
    }

    private void OnTowerButtonPressed(TowerType towerType, Guid playerTowerId, string towerName)
    {
        if (!_matchStarted)
        {
            GD.Print("Match not started yet");
            return;
        }

        // Select tower type for placement
        _selectedTowerType = towerType;
        _selectedPlayerTowerId = playerTowerId;
        var stats = TowerDefinitions.GetStats(towerType);

        if (_selectedTowerLabel != null)
        {
            _selectedTowerLabel.Text = $"Selected: {towerName} ({stats.Cost}g) - Click on grid to place";
        }

        GD.Print($"Selected tower: {towerName} (type={towerType}, id={playerTowerId})");
    }

    public override void _Process(double delta)
    {
        UpdateTowerPreview();
    }

    private void UpdateTowerPreview()
    {
        if (_towerPreview == null) return;

        if (!_selectedTowerType.HasValue || !_matchStarted)
        {
            _towerPreview.Visible = false;
            return;
        }

        var mousePos = GetGlobalMousePosition();
        var gridOffset = _gameWorld.GridOffset;
        var gridWidth = _gameWorld.GridWidth * GameConstants.GridCellSize;
        var gridHeight = _gameWorld.GridHeight * GameConstants.GridCellSize;

        // Check if mouse is within grid bounds
        if (mousePos.X >= gridOffset.X && mousePos.X < gridOffset.X + gridWidth &&
            mousePos.Y >= gridOffset.Y && mousePos.Y < gridOffset.Y + gridHeight)
        {
            // Snap to grid center
            var snapped = _gameWorld.SnapToGrid(mousePos);
            _towerPreview.Position = snapped;
            _towerPreview.Visible = true;

            // Green for valid, red for invalid placement
            var canPlace = _gameWorld.CanPlaceTower(mousePos);
            _towerPreview.Modulate = canPlace
                ? new Color(0.3f, 1.0f, 0.3f, 0.5f)
                : new Color(1.0f, 0.3f, 0.3f, 0.5f);
        }
        else
        {
            _towerPreview.Visible = false;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            _selectedTowerType.HasValue &&
            _matchStarted)
        {
            // Check if click is in the game area (not on UI)
            var mousePos = GetGlobalMousePosition();

            // Get grid offset from GameWorld
            var gridOffset = _gameWorld.GridOffset;
            var gridWidth = _gameWorld.GridWidth * GameConstants.GridCellSize;
            var gridHeight = _gameWorld.GridHeight * GameConstants.GridCellSize;

            // Check if click is within the grid bounds
            if (mousePos.X >= gridOffset.X && mousePos.X < gridOffset.X + gridWidth &&
                mousePos.Y >= gridOffset.Y && mousePos.Y < gridOffset.Y + gridHeight)
            {
                PlaceTower(mousePos);
            }
        }

        // Right click to deselect
        if (@event is InputEventMouseButton rightClick &&
            rightClick.Pressed &&
            rightClick.ButtonIndex == MouseButton.Right)
        {
            _selectedTowerType = null;
            if (_selectedTowerLabel != null)
            {
                _selectedTowerLabel.Text = "Click tower to select, then click on grid to place";
            }
            if (_towerPreview != null)
            {
                _towerPreview.Visible = false;
            }
        }
    }

    private void PlaceTower(Vector2 mousePos)
    {
        if (!_selectedTowerType.HasValue) return;

        var towerType = _selectedTowerType.Value;

        // Convert mouse position to grid coordinates (accounting for offset)
        var gridOffset = _gameWorld.GridOffset;
        var localPos = mousePos - gridOffset;
        var gridX = Mathf.FloorToInt(localPos.X / GameConstants.GridCellSize);
        var gridY = Mathf.FloorToInt(localPos.Y / GameConstants.GridCellSize);

        // Client-side gold validation (server will validate too)
        var stats = TowerDefinitions.GetStats(towerType);
        if (_gold < stats.Cost)
        {
            ShowNotEnoughGold();
            return;
        }

        // Client-side placement validation
        if (!_gameWorld.CanPlaceTower(mousePos))
        {
            ShowInvalidPlacement();
            return;
        }

        GD.Print($"Placing tower {towerType} at grid ({gridX}, {gridY})");

        // Send build request to server
        _network?.SendTowerBuild(towerType, gridX, gridY, _selectedPlayerTowerId);

        // Keep tower selected for multiple placements (optional: clear selection)
        // _selectedTowerType = null;
    }

    private void UpdateStatus(string status)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = status;
        }
    }

    private void UpdateUI()
    {
        if (_waveLabel != null)
            _waveLabel.Text = $"Wave: {_currentWave}";
        if (_livesLabel != null)
            _livesLabel.Text = $"Lives: {_lives}";
        if (_goldLabel != null)
            _goldLabel.Text = $"Gold: {_gold}";
    }

    private void ShowNotEnoughGold()
    {
        if (_goldLabel != null)
        {
            _goldLabel.AddThemeColorOverride("font_color", new Color(1, 0.3f, 0.3f));

            GetTree().CreateTimer(1.0f).Timeout += () =>
            {
                _goldLabel?.RemoveThemeColorOverride("font_color");
            };
        }
    }

    private void ShowInvalidPlacement()
    {
        // Could show visual feedback for invalid placement
        GD.Print("Invalid tower placement!");
    }

    private void ShowVictoryScreen()
    {
        FreeEndGamePopup();
        _endGamePopup = new AcceptDialog
        {
            Title = "Victory!",
            DialogText = $"Congratulations! You completed Tier {_currentTier}!\nScore: {_score}",
            DialogAutowrap = true
        };
        _endGamePopup.Confirmed += ReturnToTierSelection;
        _endGamePopup.Canceled += ReturnToTierSelection;
        AddChild(_endGamePopup);
        _endGamePopup.PopupCentered();
    }

    private void ShowGameOverScreen()
    {
        FreeEndGamePopup();
        _endGamePopup = new AcceptDialog
        {
            Title = "Game Over",
            DialogText = $"You were defeated on wave {_currentWave}.\nScore: {_score}\nTry again!",
            DialogAutowrap = true
        };
        _endGamePopup.Confirmed += ReturnToTierSelection;
        _endGamePopup.Canceled += ReturnToTierSelection;
        AddChild(_endGamePopup);
        _endGamePopup.PopupCentered();
    }

    private void FreeEndGamePopup()
    {
        if (_endGamePopup != null && IsInstanceValid(_endGamePopup))
        {
            _endGamePopup.Hide();
            _endGamePopup.QueueFree();
            _endGamePopup = null;
        }
    }

    private void ReturnToTierSelection()
    {
        // Re-enable auto scene switch for other modes
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AutoSceneSwitch = true;
        }

        _network?.Disconnect();
        GetTree().ChangeSceneToFile("res://scenes/tier_selection.tscn");
    }

    private void OnBackPressed()
    {
        ReturnToTierSelection();
    }
}
