using Godot;
using TowerWars.Client.Autoloads;
using TowerWars.Client.Auth;

namespace TowerWars.Client.UI;

public partial class MainMenu : Control
{
    private LineEdit? _addressInput;
    private LineEdit? _portInput;
    private Button? _connectButton;
    private Button? _loginButton;
    private Button? _quitButton;
    private Label? _statusLabel;
    private Label? _authStatusLabel;

    private OidcAuthManager? _authManager;

    public override void _Ready()
    {
        _addressInput = GetNodeOrNull<LineEdit>("VBoxContainer/AddressInput");
        _portInput = GetNodeOrNull<LineEdit>("VBoxContainer/PortInput");
        _connectButton = GetNodeOrNull<Button>("VBoxContainer/ConnectButton");
        _loginButton = GetNodeOrNull<Button>("VBoxContainer/LoginButton");
        _quitButton = GetNodeOrNull<Button>("VBoxContainer/QuitButton");
        _statusLabel = GetNodeOrNull<Label>("VBoxContainer/StatusLabel");
        _authStatusLabel = GetNodeOrNull<Label>("VBoxContainer/AuthStatusLabel");

        if (_addressInput != null) _addressInput.Text = "localhost";
        if (_portInput != null) _portInput.Text = "7100";

        // Setup auth manager
        _authManager = new OidcAuthManager();
        AddChild(_authManager);
        _authManager.AuthenticationCompleted += OnAuthenticationCompleted;
        _authManager.AuthenticationFailed += OnAuthenticationFailed;

        _connectButton?.Connect("pressed", Callable.From(OnConnectPressed));
        _loginButton?.Connect("pressed", Callable.From(OnLoginPressed));
        _quitButton?.Connect("pressed", Callable.From(OnQuitPressed));

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Network.Connected += OnConnected;
            GameManager.Instance.Network.Disconnected += OnDisconnected;
        }

        UpdateAuthStatus();
    }

    public override void _ExitTree()
    {
        if (_authManager != null)
        {
            _authManager.AuthenticationCompleted -= OnAuthenticationCompleted;
            _authManager.AuthenticationFailed -= OnAuthenticationFailed;
        }

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

    private async void OnLoginPressed()
    {
        if (_authManager == null) return;

        UpdateAuthStatus("Logging in...");
        _loginButton.Disabled = true;

        var success = await _authManager.LoginAsync();

        _loginButton.Disabled = false;
        UpdateAuthStatus();
    }

    private void OnAuthenticationCompleted(bool success)
    {
        if (success)
        {
            UpdateAuthStatus("Authenticated ✓");
            GD.Print($"Access Token: {_authManager?.AccessToken?.Substring(0, 20)}...");
        }
        else
        {
            UpdateAuthStatus("Authentication failed");
        }
    }

    private void OnAuthenticationFailed(string error)
    {
        UpdateAuthStatus($"Auth error: {error}");
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

    private void UpdateAuthStatus(string? message = null)
    {
        if (_authStatusLabel == null) return;

        if (message != null)
        {
            _authStatusLabel.Text = message;
        }
        else if (_authManager?.IsAuthenticated == true)
        {
            _authStatusLabel.Text = "Authenticated ✓";
            _authStatusLabel.Modulate = new Color(0.3f, 1f, 0.3f);
        }
        else
        {
            _authStatusLabel.Text = "Not authenticated";
            _authStatusLabel.Modulate = new Color(1f, 0.5f, 0.3f);
        }
    }
}
