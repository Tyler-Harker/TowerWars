using Godot;
using System.Collections.Generic;
using TowerWars.Client.Networking;
using TowerWars.Shared.Constants;
using TowerWars.Shared.DTOs;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.Game;

public partial class GameWorld : Node2D
{
    [Export]
    public PackedScene? TowerScene { get; set; }

    [Export]
    public PackedScene? UnitScene { get; set; }

    [Export]
    public int GridWidth { get; set; } = GameConstants.DefaultMapWidth;

    [Export]
    public int GridHeight { get; set; } = GameConstants.DefaultMapHeight;

    private readonly Dictionary<uint, Node2D> _entities = new();
    private readonly Dictionary<uint, ItemDropNode> _itemDrops = new();
    private PacketHandler? _packetHandler;
    private NetworkManager? _network;

    private int _gold;
    private int _lives;
    private int _score;
    private int _currentWave;
    private Vector2 _gridOffset;

    public int Gold => _gold;
    public int Lives => _lives;
    public int Score => _score;
    public int CurrentWave => _currentWave;

    public override void _Ready()
    {
        CalculateGridOffset();
        _network = GetNode<NetworkManager>("/root/GameManager/NetworkManager");
        _packetHandler = GetNode<PacketHandler>("/root/GameManager/PacketHandler");

        _packetHandler.EntitySpawned += OnEntitySpawnedWithData;
        _packetHandler.EntityDestroyed += OnEntityDestroyed;
        _packetHandler.EntityUpdated += OnEntityUpdated;
        _packetHandler.PlayerStateUpdated += OnPlayerStateUpdated;
        _packetHandler.WaveStarted += OnWaveStarted;
        _packetHandler.WaveEnded += OnWaveEnded;
        _packetHandler.ItemDropped += OnItemDropped;
        _packetHandler.ItemCollected += OnItemCollected;
    }

    public override void _ExitTree()
    {
        if (_packetHandler != null)
        {
            _packetHandler.EntitySpawned -= OnEntitySpawnedWithData;
            _packetHandler.EntityDestroyed -= OnEntityDestroyed;
            _packetHandler.EntityUpdated -= OnEntityUpdated;
            _packetHandler.PlayerStateUpdated -= OnPlayerStateUpdated;
            _packetHandler.WaveStarted -= OnWaveStarted;
            _packetHandler.WaveEnded -= OnWaveEnded;
            _packetHandler.ItemDropped -= OnItemDropped;
            _packetHandler.ItemCollected -= OnItemCollected;
        }
    }

    public void RequestBuildTower(TowerType type, Vector2 position)
    {
        // Translate from client screen position to grid coordinates
        var localPos = position - _gridOffset;
        var gridX = Mathf.FloorToInt(localPos.X / GameConstants.GridCellSize);
        var gridY = Mathf.FloorToInt(localPos.Y / GameConstants.GridCellSize);

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
        var localPos = position - _gridOffset;
        var gridX = Mathf.FloorToInt(localPos.X / GameConstants.GridCellSize);
        var gridY = Mathf.FloorToInt(localPos.Y / GameConstants.GridCellSize);

        return new Vector2(
            _gridOffset.X + gridX * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f,
            _gridOffset.Y + gridY * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f
        );
    }

    public bool CanPlaceTower(Vector2 position)
    {
        var localPos = position - _gridOffset;
        var gridX = Mathf.FloorToInt(localPos.X / GameConstants.GridCellSize);
        var gridY = Mathf.FloorToInt(localPos.Y / GameConstants.GridCellSize);

        if (gridX < 0 || gridX >= GridWidth) return false;
        if (gridY < 0 || gridY >= GridHeight) return false;

        // Block the path row (enemies walk on this row)
        if (gridY == GridHeight / 2) return false;

        // Check for existing towers (their positions are already in client space)
        foreach (var entity in _entities.Values)
        {
            if (entity is Tower tower)
            {
                var towerLocalPos = tower.Position - _gridOffset;
                var towerGridX = Mathf.FloorToInt(towerLocalPos.X / GameConstants.GridCellSize);
                var towerGridY = Mathf.FloorToInt(towerLocalPos.Y / GameConstants.GridCellSize);

                if (towerGridX == gridX && towerGridY == gridY)
                    return false;
            }
        }

        return true;
    }

    private void OnEntitySpawnedWithData(uint entityId, int entityType, float x, float y, byte[] extraData)
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

                // Apply unit rarity color from ExtraData
                if (type == EntityType.Unit && extraData.Length >= 2)
                {
                    var unitRarity = (UnitRarity)extraData[1];
                    placeholder.Modulate = GetUnitRarityColor(unitRarity);

                    // Make rare units slightly larger
                    if (unitRarity == UnitRarity.Rare)
                        placeholder.Scale *= 1.3f;
                    else if (unitRarity == UnitRarity.Magic)
                        placeholder.Scale *= 1.15f;
                }

                entity = placeholder;
                break;
        }

        if (entity != null)
        {
            // Translate server position to client grid (server assumes 0,0 origin, client centers grid)
            entity.Position = new Vector2(x, y) + _gridOffset;
            entity.Name = $"Entity_{entityId}";
            AddChild(entity);
            _entities[entityId] = entity;
        }
    }

    private static Color GetUnitRarityColor(UnitRarity rarity) => rarity switch
    {
        UnitRarity.Normal => Colors.White,
        UnitRarity.Magic => new Color(0.4f, 0.6f, 1.0f),  // Blue
        UnitRarity.Rare => new Color(1.0f, 1.0f, 0.4f),   // Yellow
        _ => Colors.White
    };

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
            // Translate server position to client grid
            var targetPos = new Vector2(x, y) + _gridOffset;
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

    private void OnItemDropped(uint dropId, float x, float y, int itemType, int rarity, int itemLevel, string name, uint ownerId)
    {
        var itemDrop = new ItemDropNode
        {
            DropId = dropId,
            ItemType = (ItemType)itemType,
            Rarity = (ItemRarity)rarity,
            ItemLevel = itemLevel,
            ItemName = name,
            OwnerId = ownerId,
            // Translate server position to client grid
            Position = new Vector2(x, y) + _gridOffset,
            Name = $"ItemDrop_{dropId}"
        };

        AddChild(itemDrop);
        _itemDrops[dropId] = itemDrop;

        GD.Print($"Item drop spawned: {name} ({(ItemRarity)rarity}) at ({x}, {y})");
    }

    private void OnItemCollected(uint dropId, bool success, string? itemId, string? error)
    {
        if (success && _itemDrops.TryGetValue(dropId, out var itemDrop))
        {
            // ItemDropNode handles its own cleanup when collection succeeds
            _itemDrops.Remove(dropId);
        }
    }

    private void CalculateGridOffset()
    {
        var viewportSize = GetViewportRect().Size;
        var gridPixelWidth = GridWidth * GameConstants.GridCellSize;
        var gridPixelHeight = GridHeight * GameConstants.GridCellSize;
        _gridOffset = new Vector2(
            (viewportSize.X - gridPixelWidth) / 2,
            (viewportSize.Y - gridPixelHeight) / 2
        );
    }

    public override void _Draw()
    {
        DrawGrid();
    }

    private void DrawGrid()
    {
        var cellSize = GameConstants.GridCellSize;

        // Colors
        var backgroundColor = new Color(0.15f, 0.18f, 0.22f);
        var borderColor = new Color(0.4f, 0.5f, 0.6f);
        var spawnColor = new Color(0.2f, 0.7f, 0.3f, 0.8f);
        var goalColor = new Color(0.8f, 0.25f, 0.25f, 0.8f);
        var pathColor = new Color(0.35f, 0.3f, 0.25f);  // Darker path for enemy walk area
        var gridLineColor = new Color(0.3f, 0.35f, 0.4f, 0.6f);
        var cellColor = new Color(0.2f, 0.24f, 0.28f);
        var cellAltColor = new Color(0.22f, 0.26f, 0.3f);

        var gridPixelWidth = GridWidth * cellSize;
        var gridPixelHeight = GridHeight * cellSize;
        var padding = 10f;

        // Spawn and goal positions (horizontal path - spawn on left, goal on right)
        var spawnX = 0;
        var spawnY = GridHeight / 2;
        var goalX = GridWidth - 1;
        var goalY = GridHeight / 2;

        // Draw outer background/border
        var outerRect = new Rect2(
            _gridOffset.X - padding,
            _gridOffset.Y - padding,
            gridPixelWidth + padding * 2,
            gridPixelHeight + padding * 2
        );
        DrawRect(outerRect, borderColor);

        // Draw inner background
        var innerRect = new Rect2(
            _gridOffset.X,
            _gridOffset.Y,
            gridPixelWidth,
            gridPixelHeight
        );
        DrawRect(innerRect, backgroundColor);

        // Draw cells
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                var rect = new Rect2(
                    _gridOffset.X + x * cellSize,
                    _gridOffset.Y + y * cellSize,
                    cellSize,
                    cellSize
                );

                // Checkerboard pattern for cells
                var isAltCell = (x + y) % 2 == 0;
                DrawRect(rect, isAltCell ? cellColor : cellAltColor);

                // Path row (where enemies walk) - draw darker but not spawn/goal
                if (y == spawnY && x != spawnX && x != goalX)
                {
                    DrawRect(rect, pathColor);
                }

                // Spawn cell
                if (x == spawnX && y == spawnY)
                {
                    DrawRect(rect, spawnColor);
                    // Draw spawn indicator (arrow pointing right toward goal)
                    var center = rect.Position + rect.Size / 2;
                    var arrowSize = cellSize * 0.3f;
                    DrawLine(center - new Vector2(arrowSize, 0), center + new Vector2(arrowSize, 0), Colors.White, 3);
                    DrawLine(center + new Vector2(arrowSize, 0), center + new Vector2(arrowSize * 0.5f, -arrowSize * 0.5f), Colors.White, 3);
                    DrawLine(center + new Vector2(arrowSize, 0), center + new Vector2(arrowSize * 0.5f, arrowSize * 0.5f), Colors.White, 3);
                }
                // Goal cell
                else if (x == goalX && y == goalY)
                {
                    DrawRect(rect, goalColor);
                    // Draw goal indicator (X mark)
                    var center = rect.Position + rect.Size / 2;
                    var markSize = cellSize * 0.25f;
                    DrawLine(center - new Vector2(markSize, markSize), center + new Vector2(markSize, markSize), Colors.White, 3);
                    DrawLine(center - new Vector2(-markSize, markSize), center + new Vector2(-markSize, -markSize), Colors.White, 3);
                }

                // Draw grid lines
                DrawRect(rect, gridLineColor, false, 1.0f);
            }
        }

        // Draw corner decorations
        var cornerSize = 15f;
        var cornerColor = new Color(0.5f, 0.6f, 0.7f);

        // Top-left corner
        DrawLine(new Vector2(_gridOffset.X - padding, _gridOffset.Y - padding + cornerSize),
                 new Vector2(_gridOffset.X - padding, _gridOffset.Y - padding), cornerColor, 3);
        DrawLine(new Vector2(_gridOffset.X - padding, _gridOffset.Y - padding),
                 new Vector2(_gridOffset.X - padding + cornerSize, _gridOffset.Y - padding), cornerColor, 3);

        // Top-right corner
        DrawLine(new Vector2(_gridOffset.X + gridPixelWidth + padding, _gridOffset.Y - padding + cornerSize),
                 new Vector2(_gridOffset.X + gridPixelWidth + padding, _gridOffset.Y - padding), cornerColor, 3);
        DrawLine(new Vector2(_gridOffset.X + gridPixelWidth + padding, _gridOffset.Y - padding),
                 new Vector2(_gridOffset.X + gridPixelWidth + padding - cornerSize, _gridOffset.Y - padding), cornerColor, 3);

        // Bottom-left corner
        DrawLine(new Vector2(_gridOffset.X - padding, _gridOffset.Y + gridPixelHeight + padding - cornerSize),
                 new Vector2(_gridOffset.X - padding, _gridOffset.Y + gridPixelHeight + padding), cornerColor, 3);
        DrawLine(new Vector2(_gridOffset.X - padding, _gridOffset.Y + gridPixelHeight + padding),
                 new Vector2(_gridOffset.X - padding + cornerSize, _gridOffset.Y + gridPixelHeight + padding), cornerColor, 3);

        // Bottom-right corner
        DrawLine(new Vector2(_gridOffset.X + gridPixelWidth + padding, _gridOffset.Y + gridPixelHeight + padding - cornerSize),
                 new Vector2(_gridOffset.X + gridPixelWidth + padding, _gridOffset.Y + gridPixelHeight + padding), cornerColor, 3);
        DrawLine(new Vector2(_gridOffset.X + gridPixelWidth + padding, _gridOffset.Y + gridPixelHeight + padding),
                 new Vector2(_gridOffset.X + gridPixelWidth + padding - cornerSize, _gridOffset.Y + gridPixelHeight + padding), cornerColor, 3);
    }

    /// <summary>
    /// Convert world position to grid coordinates
    /// </summary>
    public Vector2I WorldToGrid(Vector2 worldPos)
    {
        var localPos = worldPos - _gridOffset;
        return new Vector2I(
            Mathf.FloorToInt(localPos.X / GameConstants.GridCellSize),
            Mathf.FloorToInt(localPos.Y / GameConstants.GridCellSize)
        );
    }

    /// <summary>
    /// Convert grid coordinates to world position (center of cell)
    /// </summary>
    public Vector2 GridToWorld(Vector2I gridPos)
    {
        return new Vector2(
            _gridOffset.X + gridPos.X * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f,
            _gridOffset.Y + gridPos.Y * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f
        );
    }

    public Vector2 GridOffset => _gridOffset;
}
