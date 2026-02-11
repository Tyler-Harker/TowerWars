using Godot;
using TowerWars.Client.Autoloads;

namespace TowerWars.Client.UI;

public partial class MainMenu : Control
{
    private LineEdit? _addressInput;
    private LineEdit? _portInput;
    private Button? _connectButton;
    private Button? _quitButton;
    private Label? _statusLabel;

    public override void _Ready()
    {
        _addressInput = GetNodeOrNull<LineEdit>("VBoxContainer/AddressInput");
        _portInput = GetNodeOrNull<LineEdit>("VBoxContainer/PortInput");
        _connectButton = GetNodeOrNull<Button>("VBoxContainer/ConnectButton");
        _quitButton = GetNodeOrNull<Button>("VBoxContainer/QuitButton");
        _statusLabel = GetNodeOrNull<Label>("VBoxContainer/StatusLabel");

        if (_addressInput != null) _addressInput.Text = "localhost";
        if (_portInput != null) _portInput.Text = "7100";

        _connectButton?.Connect("pressed", Callable.From(OnConnectPressed));
        _quitButton?.Connect("pressed", Callable.From(OnQuitPressed));

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Network.Connected += OnConnected;
            GameManager.Instance.Network.Disconnected += OnDisconnected;
        }
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Network.Connected -= OnConnected;
            GameManager.Instance.Network.Disconnected -= OnDisconnected;
        }
    }

    private void OnConnectPressed()
    {
        var address = _addressInput?.Text ?? "localhost";
        var port = ushort.TryParse(_portInput?.Text, out var p) ? p : (ushort)7100;

        UpdateStatus("Connecting...");
        GameManager.Instance?.ConnectToServer(address, port);
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void OnConnected()
    {
        UpdateStatus("Connected!");
    }

    private void OnDisconnected(string reason)
    {
        UpdateStatus($"Disconnected: {reason}");
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.Text = message;
    }
}
