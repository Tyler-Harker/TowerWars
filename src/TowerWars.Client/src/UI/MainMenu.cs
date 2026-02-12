using Godot;
using TowerWars.Client.Autoloads;
using TowerWars.Client.Auth;

namespace TowerWars.Client.UI;

public partial class MainMenu : Control
{
    private Button? _loginButton;
    private Button? _quitButton;
    private Label? _statusLabel;

    private OidcAuthManager? _authManager;

    public override void _Ready()
    {
        _loginButton = GetNodeOrNull<Button>("VBoxContainer/LoginButton");
        _quitButton = GetNodeOrNull<Button>("VBoxContainer/QuitButton");
        _statusLabel = GetNodeOrNull<Label>("VBoxContainer/StatusLabel");

        // Setup auth manager
        _authManager = new OidcAuthManager();
        AddChild(_authManager);
        _authManager.AuthenticationCompleted += OnAuthenticationCompleted;
        _authManager.AuthenticationFailed += OnAuthenticationFailed;

        _loginButton?.Connect("pressed", Callable.From(OnLoginPressed));
        _quitButton?.Connect("pressed", Callable.From(OnQuitPressed));

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Network.Connected += OnConnected;
            GameManager.Instance.Network.Disconnected += OnDisconnected;
        }

        UpdateStatus("Click Login to start");
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

    private async void OnLoginPressed()
    {
        if (_authManager == null) return;

        UpdateStatus("Opening browser for login...");
        if (_loginButton != null) _loginButton.Disabled = true;

        var success = await _authManager.LoginAsync();

        if (_loginButton != null) _loginButton.Disabled = false;
    }

    private void OnAuthenticationCompleted(bool success)
    {
        if (success)
        {
            UpdateStatus("Authenticated!");

            // Store the auth token and configure the API client
            if (GameManager.Instance != null && _authManager?.AccessToken != null)
            {
                GameManager.Instance.SetAuthToken(_authManager.AccessToken);
            }

            GetTree().ChangeSceneToFile("res://scenes/dashboard.tscn");
        }
        else
        {
            UpdateStatus("Authentication failed");
        }
    }

    private void OnAuthenticationFailed(string error)
    {
        UpdateStatus($"Login failed: {error}");
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
