using Godot;
using TowerWars.Client.Autoloads;
using TowerWars.Client.Networking;
using TowerWars.Shared.DTOs;

namespace TowerWars.Client.Game;

/// <summary>
/// Server-driven item drop visual that renders items dropped by the server
/// and sends collection requests via the network protocol.
/// </summary>
public partial class ItemDropNode : Node2D
{
    public uint DropId { get; set; }
    public ItemType ItemType { get; set; }
    public ItemRarity Rarity { get; set; }
    public int ItemLevel { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public uint OwnerId { get; set; }

    private float _bobOffset;
    private float _bobSpeed = 3f;
    private bool _isHovered;
    private bool _isCollecting;

    private NetworkManager? _networkManager;
    private PacketHandler? _packetHandler;

    public override void _Ready()
    {
        _bobOffset = GD.Randf() * Mathf.Tau;

        _networkManager = GetNode<NetworkManager>("/root/GameManager/NetworkManager");
        _packetHandler = GetNode<PacketHandler>("/root/GameManager/PacketHandler");

        if (_packetHandler != null)
        {
            _packetHandler.ItemCollected += OnItemCollected;
        }
    }

    public override void _ExitTree()
    {
        if (_packetHandler != null)
        {
            _packetHandler.ItemCollected -= OnItemCollected;
        }
    }

    public override void _Process(double delta)
    {
        _bobOffset += _bobSpeed * (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var bob = Mathf.Sin(_bobOffset) * 3f;
        var center = new Vector2(0, bob);

        var rarityColor = GetRarityColor(Rarity);

        // Glow effect for higher rarities
        if (Rarity >= ItemRarity.Magic)
        {
            var glowSize = Rarity switch
            {
                ItemRarity.Magic => 20f,
                ItemRarity.Rare => 25f,
                _ => 15f
            };
            var glowColor = new Color(rarityColor.R, rarityColor.G, rarityColor.B, 0.3f);
            DrawCircle(center, glowSize, glowColor);
        }

        // Item background
        var bgSize = 16f;
        DrawCircle(center, bgSize, new Color(0.1f, 0.1f, 0.15f));
        DrawCircle(center, bgSize, rarityColor, false, 2f);

        // Item icon based on type
        DrawItemIcon(center, ItemType);

        // Hover tooltip
        if (_isHovered)
        {
            DrawTooltip();
        }

        // Collecting indicator
        if (_isCollecting)
        {
            DrawCircle(center, bgSize + 8, new Color(1, 1, 1, 0.5f), false, 3f);
        }

        // Pickup pulse effect
        var pulseAlpha = (Mathf.Sin(_bobOffset * 2) + 1) * 0.25f + 0.5f;
        DrawCircle(center, bgSize + 4, new Color(rarityColor.R, rarityColor.G, rarityColor.B, pulseAlpha * 0.3f), false, 2f);
    }

    private static Color GetRarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Normal => new Color(0.8f, 0.8f, 0.8f),
        ItemRarity.Magic => new Color(0.4f, 0.6f, 1.0f),
        ItemRarity.Rare => new Color(1.0f, 1.0f, 0.4f),
        _ => new Color(1f, 1f, 1f)
    };

    private void DrawItemIcon(Vector2 center, ItemType type)
    {
        var iconColor = new Color(0.9f, 0.9f, 0.9f);

        switch (type)
        {
            case ItemType.Weapon:
                // Sword icon
                DrawLine(center + new Vector2(-6, 6), center + new Vector2(6, -6), iconColor, 2);
                DrawLine(center + new Vector2(4, -4), center + new Vector2(8, -8), iconColor, 3);
                DrawLine(center + new Vector2(-2, 2), center + new Vector2(2, -2), iconColor, 4);
                break;

            case ItemType.Shield:
                // Shield icon
                var shieldPoints = new Vector2[]
                {
                    center + new Vector2(0, -7),
                    center + new Vector2(6, -4),
                    center + new Vector2(6, 2),
                    center + new Vector2(0, 7),
                    center + new Vector2(-6, 2),
                    center + new Vector2(-6, -4)
                };
                DrawPolygon(shieldPoints, new[] { iconColor });
                break;

            case ItemType.Accessory:
                // Ring/circle icon
                DrawCircle(center, 5, iconColor, false, 2);
                DrawCircle(center + new Vector2(0, -6), 3, iconColor);
                break;

            default:
                DrawCircle(center, 6, iconColor);
                break;
        }
    }

    private void DrawTooltip()
    {
        var tooltipPos = new Vector2(25, -50);
        var lineHeight = 18f;
        var padding = 8f;
        var fontSize = 14;

        var lines = new System.Collections.Generic.List<string>
        {
            ItemName,
            $"Item Level: {ItemLevel}",
            $"Type: {ItemType}",
            $"Rarity: {Rarity}"
        };

        var maxWidth = 180f;
        var height = lines.Count * lineHeight + padding * 2;

        // Background
        var bgRect = new Rect2(tooltipPos, new Vector2(maxWidth, height));
        DrawRect(bgRect, new Color(0.1f, 0.1f, 0.15f, 0.95f));
        var rarityColor = GetRarityColor(Rarity);
        DrawRect(bgRect, rarityColor, false, 2f);

        // Draw text
        var font = ThemeDB.FallbackFont;
        var textColor = Colors.White;
        var nameColor = rarityColor;

        for (int i = 0; i < lines.Count; i++)
        {
            var textPos = tooltipPos + new Vector2(padding, padding + (i + 1) * lineHeight - 4);
            var color = i == 0 ? nameColor : textColor;
            DrawString(font, textPos, lines[i], HorizontalAlignment.Left, maxWidth - padding * 2, fontSize, color);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_isCollecting) return;

        if (@event is InputEventMouseMotion motion)
        {
            var localPos = ToLocal(motion.GlobalPosition);
            _isHovered = localPos.Length() < 20f;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            var localPos = ToLocal(mouseButton.GlobalPosition);
            if (localPos.Length() < 25f)
            {
                TryCollect();
            }
        }
    }

    private void TryCollect()
    {
        if (_isCollecting) return;

        // Check if this is the current player's item
        if (_networkManager != null && _networkManager.PlayerId != OwnerId)
        {
            GD.Print($"Cannot collect item - belongs to player {OwnerId}, not {_networkManager.PlayerId}");
            return;
        }

        _isCollecting = true;
        GD.Print($"Requesting collection of item: {ItemName} (DropId: {DropId})");

        _networkManager?.SendItemCollect(DropId);
    }

    private void OnItemCollected(uint dropId, bool success, string? itemId, string? error)
    {
        if (dropId != DropId) return;

        if (success)
        {
            GD.Print($"Successfully collected: {ItemName} (ItemId: {itemId})");

            // Create pickup effect
            var effect = new ItemPickupEffect
            {
                Position = Position,
                Color = GetRarityColor(Rarity)
            };
            GetParent()?.AddChild(effect);

            QueueFree();
        }
        else
        {
            GD.PrintErr($"Failed to collect item: {error}");
            _isCollecting = false;
        }
    }
}

/// <summary>
/// Visual effect shown when an item is picked up
/// </summary>
public partial class ItemPickupEffect : Node2D
{
    public Color Color { get; set; } = Colors.White;

    private float _lifetime = 0.5f;
    private float _elapsed;

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        if (_elapsed >= _lifetime)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var progress = _elapsed / _lifetime;
        var radius = 20f + progress * 30f;
        var alpha = 1f - progress;

        DrawCircle(Vector2.Zero, radius, new Color(Color.R, Color.G, Color.B, alpha * 0.5f), false, 3f);

        // Particles flying up
        for (int i = 0; i < 5; i++)
        {
            var angle = i * Mathf.Tau / 5 + progress * Mathf.Pi;
            var dist = progress * 40f;
            var particlePos = Vector2.FromAngle(angle) * dist + new Vector2(0, -progress * 30f);
            var particleAlpha = alpha * 0.8f;
            DrawCircle(particlePos, 3f * (1f - progress), new Color(Color.R, Color.G, Color.B, particleAlpha));
        }
    }
}
