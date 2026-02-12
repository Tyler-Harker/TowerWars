using Godot;

namespace TowerWars.Client.UI;

public partial class ModeSelection : Control
{
    private Button? _soloButton;
    private Button? _coopButton;
    private Button? _pvpButton;
    private Button? _backButton;

    public override void _Ready()
    {
        _soloButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/ModesContainer/SoloButton");
        _coopButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/ModesContainer/CoopButton");
        _pvpButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/ModesContainer/PvPButton");
        _backButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/BackButton");

        _soloButton?.Connect("pressed", Callable.From(OnSoloPressed));
        _coopButton?.Connect("pressed", Callable.From(OnCoopPressed));
        _pvpButton?.Connect("pressed", Callable.From(OnPvPPressed));
        _backButton?.Connect("pressed", Callable.From(OnBackPressed));

        // Disable modes that aren't implemented yet
        if (_coopButton != null)
        {
            _coopButton.Disabled = true;
            _coopButton.TooltipText = "Coming Soon!";
        }
        if (_pvpButton != null)
        {
            _pvpButton.Disabled = true;
            _pvpButton.TooltipText = "Coming Soon!";
        }
    }

    private void OnSoloPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/tier_selection.tscn");
    }

    private void OnCoopPressed()
    {
        GD.Print("Co-op mode not implemented yet");
    }

    private void OnPvPPressed()
    {
        GD.Print("PvP mode not implemented yet");
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/dashboard.tscn");
    }
}
