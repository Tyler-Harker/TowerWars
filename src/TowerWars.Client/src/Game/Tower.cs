using Godot;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.Game;

public partial class Tower : Node2D
{
    [Export]
    public uint EntityId { get; set; }

    [Export]
    public TowerType TowerType { get; set; }

    [Export]
    public uint OwnerId { get; set; }

    [Export]
    public int Health { get; set; }

    [Export]
    public int MaxHealth { get; set; }

    [Export]
    public float Range { get; set; }

    private Sprite2D? _sprite;
    private Area2D? _rangeIndicator;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _rangeIndicator = GetNodeOrNull<Area2D>("RangeIndicator");

        if (_rangeIndicator != null)
        {
            var collision = _rangeIndicator.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (collision?.Shape is CircleShape2D circle)
            {
                circle.Radius = Range;
            }
        }
    }

    public void SetSelected(bool selected)
    {
        if (_rangeIndicator != null)
        {
            _rangeIndicator.Visible = selected;
        }
    }
}
