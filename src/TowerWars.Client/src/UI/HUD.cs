using Godot;
using TowerWars.Client.Autoloads;
using TowerWars.Client.Game;
using TowerWars.Shared.Constants;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.UI;

public partial class HUD : CanvasLayer
{
    private Label? _goldLabel;
    private Label? _livesLabel;
    private Label? _scoreLabel;
    private Label? _waveLabel;
    private Label? _rttLabel;
    private Button? _readyButton;
    private ItemList? _towerList;
    private GameWorld? _gameWorld;

    private TowerType _selectedTower = TowerType.Basic;
    private double _pingTimer;

    public override void _Ready()
    {
        _goldLabel = GetNodeOrNull<Label>("TopPanel/GoldLabel");
        _livesLabel = GetNodeOrNull<Label>("TopPanel/LivesLabel");
        _scoreLabel = GetNodeOrNull<Label>("TopPanel/ScoreLabel");
        _waveLabel = GetNodeOrNull<Label>("TopPanel/WaveLabel");
        _rttLabel = GetNodeOrNull<Label>("TopPanel/RttLabel");
        _readyButton = GetNodeOrNull<Button>("BottomPanel/ReadyButton");
        _towerList = GetNodeOrNull<ItemList>("SidePanel/TowerList");

        _readyButton?.Connect("pressed", Callable.From(OnReadyPressed));

        InitializeTowerList();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PacketHandler.WaveStarted += OnWaveStarted;
        }
    }

    public override void _Process(double delta)
    {
        UpdateLabels();

        _pingTimer += delta;
        if (_pingTimer >= 1.0)
        {
            _pingTimer = 0;
            GameManager.Instance?.Network.SendPing();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                var mousePos = GetViewport().GetMousePosition();
                _gameWorld ??= GetTree().Root.GetNodeOrNull<GameWorld>("Game/GameWorld");

                if (_gameWorld != null && _gameWorld.CanPlaceTower(mousePos))
                {
                    _gameWorld.RequestBuildTower(_selectedTower, mousePos);
                }
            }
        }
    }

    private void InitializeTowerList()
    {
        if (_towerList == null) return;

        _towerList.AddItem("Basic Tower (100g)");
        _towerList.AddItem("Archer Tower (150g)");
        _towerList.AddItem("Cannon Tower (250g)");
        _towerList.AddItem("Magic Tower (200g)");
        _towerList.AddItem("Ice Tower (175g)");
        _towerList.AddItem("Fire Tower (225g)");

        _towerList.ItemSelected += OnTowerSelected;
        _towerList.Select(0);
    }

    private void OnTowerSelected(long index)
    {
        _selectedTower = (TowerType)(int)index;
    }

    private void OnReadyPressed()
    {
        GameManager.Instance?.Network.SendReady(true);
    }

    private void OnWaveStarted(int waveNumber)
    {
        if (_waveLabel != null)
            _waveLabel.Text = $"Wave: {waveNumber}";
    }

    private void UpdateLabels()
    {
        _gameWorld ??= GetTree().Root.GetNodeOrNull<GameWorld>("Game/GameWorld");

        if (_gameWorld != null)
        {
            if (_goldLabel != null) _goldLabel.Text = $"Gold: {_gameWorld.Gold}";
            if (_livesLabel != null) _livesLabel.Text = $"Lives: {_gameWorld.Lives}";
            if (_scoreLabel != null) _scoreLabel.Text = $"Score: {_gameWorld.Score}";
        }

        if (_rttLabel != null && GameManager.Instance != null)
        {
            _rttLabel.Text = $"RTT: {GameManager.Instance.Network.Rtt}ms";
        }
    }
}
