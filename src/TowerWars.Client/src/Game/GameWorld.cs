using Godot;
using System.Collections.Generic;
using TowerWars.Client.Networking;
using TowerWars.Shared.Constants;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.Game;

public partial class GameWorld : Node2D
{
    [Export]
    public PackedScene? TowerScene { get; set; }

    [Export]
    public PackedScene? UnitScene { get; set; }

    private readonly Dictionary<uint, Node2D> _entities = new();
    private PacketHandler? _packetHandler;
    private NetworkManager? _network;

    private int _gold;
    private int _lives;
    private int _score;
    private int _currentWave;

    public int Gold => _gold;
    public int Lives => _lives;
    public int Score => _score;
    public int CurrentWave => _currentWave;

    public override void _Ready()
    {
        _network = GetNode<NetworkManager>("/root/GameManager/NetworkManager");
        _packetHandler = GetNode<PacketHandler>("/root/GameManager/PacketHandler");

        _packetHandler.EntitySpawned += OnEntitySpawned;
        _packetHandler.EntityDestroyed += OnEntityDestroyed;
        _packetHandler.EntityUpdated += OnEntityUpdated;
        _packetHandler.PlayerStateUpdated += OnPlayerStateUpdated;
        _packetHandler.WaveStarted += OnWaveStarted;
        _packetHandler.WaveEnded += OnWaveEnded;
    }

    public override void _ExitTree()
    {
        if (_packetHandler != null)
        {
            _packetHandler.EntitySpawned -= OnEntitySpawned;
            _packetHandler.EntityDestroyed -= OnEntityDestroyed;
            _packetHandler.EntityUpdated -= OnEntityUpdated;
            _packetHandler.PlayerStateUpdated -= OnPlayerStateUpdated;
            _packetHandler.WaveStarted -= OnWaveStarted;
            _packetHandler.WaveEnded -= OnWaveEnded;
        }
    }

    public void RequestBuildTower(TowerType type, Vector2 position)
    {
        var gridX = Mathf.FloorToInt(position.X / GameConstants.GridCellSize);
        var gridY = Mathf.FloorToInt(position.Y / GameConstants.GridCellSize);

        var stats = TowerDefinitions.GetStats(type);
        if (_gold < stats.Cost)
        {
            GD.Print("Not enough gold!");
            return;
        }

        _network?.SendTowerBuild(type, gridX, gridY);
    }

    public void RequestSellTower(uint towerId)
    {
        _network?.SendTowerSell(towerId);
    }

    public Vector2 SnapToGrid(Vector2 position)
    {
        var gridX = Mathf.FloorToInt(position.X / GameConstants.GridCellSize);
        var gridY = Mathf.FloorToInt(position.Y / GameConstants.GridCellSize);

        return new Vector2(
            gridX * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f,
            gridY * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f
        );
    }

    public bool CanPlaceTower(Vector2 position)
    {
        var gridX = Mathf.FloorToInt(position.X / GameConstants.GridCellSize);
        var gridY = Mathf.FloorToInt(position.Y / GameConstants.GridCellSize);

        if (gridX < 0 || gridX >= GameConstants.DefaultMapWidth) return false;
        if (gridY < 0 || gridY >= GameConstants.DefaultMapHeight) return false;
        if (gridY == 5) return false;

        foreach (var entity in _entities.Values)
        {
            if (entity is Tower tower)
            {
                var towerGridX = Mathf.FloorToInt(tower.Position.X / GameConstants.GridCellSize);
                var towerGridY = Mathf.FloorToInt(tower.Position.Y / GameConstants.GridCellSize);

                if (towerGridX == gridX && towerGridY == gridY)
                    return false;
            }
        }

        return true;
    }

    private void OnEntitySpawned(uint entityId, int entityType, float x, float y)
    {
        var type = (EntityType)entityType;
        Node2D? entity = null;

        switch (type)
        {
            case EntityType.Tower when TowerScene != null:
                entity = TowerScene.Instantiate<Node2D>();
                break;

            case EntityType.Unit when UnitScene != null:
                entity = UnitScene.Instantiate<Node2D>();
                break;

            default:
                var placeholder = new Sprite2D();
                placeholder.Texture = GD.Load<Texture2D>("res://icon.svg");
                placeholder.Scale = type == EntityType.Tower ? new Vector2(0.5f, 0.5f) : new Vector2(0.3f, 0.3f);
                entity = placeholder;
                break;
        }

        if (entity != null)
        {
            entity.Position = new Vector2(x, y);
            entity.Name = $"Entity_{entityId}";
            AddChild(entity);
            _entities[entityId] = entity;
        }
    }

    private void OnEntityDestroyed(uint entityId)
    {
        if (_entities.TryGetValue(entityId, out var entity))
        {
            entity.QueueFree();
            _entities.Remove(entityId);
        }
    }

    private void OnEntityUpdated(uint entityId, float x, float y, int health)
    {
        if (_entities.TryGetValue(entityId, out var entity))
        {
            var targetPos = new Vector2(x, y);
            entity.Position = entity.Position.Lerp(targetPos, 0.3f);
        }
    }

    private void OnPlayerStateUpdated(uint playerId, int gold, int lives, int score)
    {
        if (_network != null && playerId == _network.PlayerId)
        {
            _gold = gold;
            _lives = lives;
            _score = score;
        }
    }

    private void OnWaveStarted(int waveNumber)
    {
        _currentWave = waveNumber;
    }

    private void OnWaveEnded(int waveNumber, bool success, int bonusGold)
    {
        if (success)
        {
            GD.Print($"Wave {waveNumber} complete! Bonus: {bonusGold} gold");
        }
    }
}
