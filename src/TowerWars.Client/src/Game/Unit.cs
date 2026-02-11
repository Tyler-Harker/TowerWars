using Godot;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.Game;

public partial class Unit : Node2D
{
    [Export]
    public uint EntityId { get; set; }

    [Export]
    public UnitType UnitType { get; set; }

    [Export]
    public int Health { get; set; }

    [Export]
    public int MaxHealth { get; set; }

    [Export]
    public float Speed { get; set; }

    private Sprite2D? _sprite;
    private ProgressBar? _healthBar;
    private Vector2 _targetPosition;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _healthBar = GetNodeOrNull<ProgressBar>("HealthBar");

        UpdateHealthBar();
    }

    public override void _Process(double delta)
    {
        if (Position.DistanceTo(_targetPosition) > 1)
        {
            Position = Position.Lerp(_targetPosition, (float)(Speed * delta * 10));
        }
    }

    public void SetTargetPosition(Vector2 target)
    {
        _targetPosition = target;
    }

    public void SetHealth(int health)
    {
        Health = health;
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (_healthBar != null && MaxHealth > 0)
        {
            _healthBar.Value = (float)Health / MaxHealth * 100;
        }
    }
}
