using Godot;
using TowerWars.Client.Networking;

namespace TowerWars.Client.Autoloads;

public partial class GameManager : Node
{
    public static GameManager? Instance { get; private set; }

    public NetworkManager Network { get; private set; } = null!;
    public PacketHandler PacketHandler { get; private set; } = null!;
    public ClientPrediction Prediction { get; private set; } = null!;

    public string? AuthToken { get; set; }
    public string? ConnectionToken { get; set; }
    public string? Username { get; set; }

    private string _serverAddress = "localhost";
    private ushort _serverPort = 7100;

    public override void _Ready()
    {
        Instance = this;

        Network = new NetworkManager();
        Network.Name = "NetworkManager";
        AddChild(Network);

        PacketHandler = new PacketHandler();
        PacketHandler.Name = "PacketHandler";
        AddChild(PacketHandler);

        Prediction = new ClientPrediction();
        Prediction.Name = "ClientPrediction";
        AddChild(Prediction);

        Network.Connected += OnConnected;
        Network.Disconnected += OnDisconnected;

        GD.Print("GameManager initialized");
    }

    public override void _ExitTree()
    {
        Instance = null;
    }

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

    public void ChangeScene(string scenePath)
    {
        GetTree().ChangeSceneToFile(scenePath);
    }

    private void OnConnected()
    {
        GD.Print("Connected to game server!");
        ChangeScene("res://scenes/game.tscn");
    }

    private void OnDisconnected(string reason)
    {
        GD.Print($"Disconnected from game server: {reason}");
        ChangeScene("res://scenes/main_menu.tscn");
    }
}
