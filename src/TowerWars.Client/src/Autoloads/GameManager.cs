using Godot;
using TowerWars.Client.Networking;
using TowerWars.Client.Services;

namespace TowerWars.Client.Autoloads;

public partial class GameManager : Node
{
    public static GameManager? Instance { get; private set; }

    public NetworkManager Network { get; private set; } = null!;
    public PacketHandler PacketHandler { get; private set; } = null!;
    public ClientPrediction Prediction { get; private set; } = null!;
    public ApiClient Api { get; private set; } = null!;

    public string? AuthToken { get; set; }
    public string? ConnectionToken { get; set; }
    public string? Username { get; set; }
    public int SelectedTier { get; set; } = 1;
    public Services.ServerTower? SelectedTower { get; set; }

    private string _serverAddress = "localhost";
    private ushort _serverPort = 7100;
    private string _gatewayUrl = "http://localhost:7000";
    private bool _autoConnect;

    public string ServerAddress => _serverAddress;
    public ushort ServerPort => _serverPort;
    public string GatewayUrl => _gatewayUrl;
    public bool AutoConnect => _autoConnect;

    public override void _Ready()
    {
        Instance = this;

        // Read configuration from environment variables (set by Aspire)
        var envAddress = OS.GetEnvironment("TOWERWARS_SERVER_ADDRESS");
        var envPort = OS.GetEnvironment("TOWERWARS_SERVER_PORT");
        var envGatewayUrl = OS.GetEnvironment("TOWERWARS_GATEWAY_URL");
        var envAutoConnect = OS.GetEnvironment("TOWERWARS_AUTO_CONNECT");

        if (!string.IsNullOrEmpty(envAddress))
            _serverAddress = envAddress;
        if (!string.IsNullOrEmpty(envPort) && ushort.TryParse(envPort, out var port))
            _serverPort = port;
        if (!string.IsNullOrEmpty(envGatewayUrl))
            _gatewayUrl = envGatewayUrl;
        _autoConnect = envAutoConnect == "true" || envAutoConnect == "1";

        GD.Print($"Server config: {_serverAddress}:{_serverPort} (auto-connect: {_autoConnect})");
        GD.Print($"Gateway URL: {_gatewayUrl}");

        Network = new NetworkManager();
        Network.Name = "NetworkManager";
        AddChild(Network);

        PacketHandler = new PacketHandler();
        PacketHandler.Name = "PacketHandler";
        AddChild(PacketHandler);

        Prediction = new ClientPrediction();
        Prediction.Name = "ClientPrediction";
        AddChild(Prediction);

        Api = new ApiClient();

        Network.Connected += OnConnected;
        Network.Disconnected += OnDisconnected;

        GD.Print("GameManager initialized");
    }

    public override void _ExitTree()
    {
        Instance = null;
    }

    /// <summary>
    /// Whether to automatically change scene on connect/disconnect.
    /// Set to false when a scene manages its own connection lifecycle.
    /// </summary>
    public bool AutoSceneSwitch { get; set; } = true;

    public void ConnectToServer(string address, ushort port)
    {
        _serverAddress = address;
        _serverPort = port;
        Network.Connect(address, port, ConnectionToken ?? "test-token");
    }

    public void ConnectToServer()
    {
        ConnectToServer(_serverAddress, _serverPort);
    }

    public void Disconnect()
    {
        Network.Disconnect();
    }

    public void SetAuthToken(string token)
    {
        AuthToken = token;
        Api.Configure(_gatewayUrl, token);
        GD.Print("API client configured with auth token");
    }

    public void ChangeScene(string scenePath)
    {
        GetTree().ChangeSceneToFile(scenePath);
    }

    private void OnConnected()
    {
        GD.Print("Connected to game server!");
        if (AutoSceneSwitch)
        {
            ChangeScene("res://scenes/game.tscn");
        }
    }

    private void OnDisconnected(string reason)
    {
        GD.Print($"Disconnected from game server: {reason}");
        if (AutoSceneSwitch)
        {
            ChangeScene("res://scenes/main_menu.tscn");
        }
    }
}
