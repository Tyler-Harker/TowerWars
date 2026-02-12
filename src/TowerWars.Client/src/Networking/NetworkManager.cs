using ENet;
using Godot;
using TowerWars.Shared.Constants;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.Networking;

public partial class NetworkManager : Node
{
    [Signal]
    public delegate void ConnectedEventHandler();

    [Signal]
    public delegate void DisconnectedEventHandler(string reason);

    [Signal]
    public delegate void PacketReceivedEventHandler(int packetType, byte[] payload);

    private Host _host = null!;
    private Peer _peer;
    private bool _connected;
    private uint _lastInputSequence;

    public bool IsConnected => _connected;
    public uint PlayerId { get; private set; }
    public uint ServerTick { get; private set; }
    public float TickRate { get; private set; }
    public long Rtt { get; private set; }

    public override void _Ready()
    {
        Library.Initialize();
        _host = new Host();
        _host.Create();
    }

    public override void _Process(double delta)
    {
        Poll();
    }

    public override void _ExitTree()
    {
        Disconnect();
        _host.Dispose();
        Library.Deinitialize();
    }

    public void Connect(string address, ushort port, string connectionToken)
    {
        if (_connected) return;

        var enetAddress = new Address();
        enetAddress.SetHost(address);
        enetAddress.Port = port;

        _peer = _host.Connect(enetAddress, 2);
        _peer.Timeout(0, 5000, 30000);

        GD.Print($"Connecting to {address}:{port}...");
    }

    public void Disconnect()
    {
        if (!_connected) return;

        _peer.DisconnectNow(0);
        _connected = false;
        EmitSignal(SignalName.Disconnected, "User disconnected");
    }

    public void SendPacket<T>(T packet, PacketFlags flags = PacketFlags.Reliable) where T : IPacket
    {
        if (!_connected) return;

        var data = PacketSerializer.Serialize(packet);
        var enetPacket = default(Packet);
        enetPacket.Create(data, flags);

        byte channel = flags.HasFlag(PacketFlags.Reliable) ? (byte)0 : (byte)1;
        _peer.Send(channel, ref enetPacket);
    }

    public void SendConnect(string connectionToken)
    {
        SendPacket(new ConnectPacket
        {
            ConnectionToken = connectionToken,
            ProtocolVersion = PacketSerializer.ProtocolVersion
        });
    }

    public void SendInput(InputFlags flags, float mouseX, float mouseY)
    {
        _lastInputSequence++;
        SendPacket(new PlayerInputPacket
        {
            InputSequence = _lastInputSequence,
            Tick = ServerTick,
            Flags = flags,
            MouseX = mouseX,
            MouseY = mouseY
        }, PacketFlags.None);
    }

    public void SendPing()
    {
        SendPacket(new PingPacket
        {
            ClientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    public void SendTowerBuild(TowerType towerType, int gridX, int gridY)
    {
        SendPacket(new TowerBuildPacket
        {
            RequestId = _lastInputSequence++,
            TowerType = towerType,
            GridX = gridX,
            GridY = gridY
        });
    }

    public void SendTowerSell(uint towerId)
    {
        SendPacket(new TowerSellPacket
        {
            RequestId = _lastInputSequence++,
            TowerId = towerId
        });
    }

    public void SendReady(bool isReady)
    {
        SendPacket(new ReadyStatePacket { IsReady = isReady });
    }

    public void SendChat(string message, ChatChannel channel = ChatChannel.Global)
    {
        SendPacket(new ChatMessagePacket
        {
            Channel = channel,
            Message = message
        });
    }

    public void SendItemCollect(uint dropId)
    {
        SendPacket(new ItemCollectPacket
        {
            RequestId = _lastInputSequence++,
            DropId = dropId
        });
    }

    private void Poll()
    {
        while (_host.Service(0, out var netEvent) > 0)
        {
            switch (netEvent.Type)
            {
                case EventType.Connect:
                    HandleConnect();
                    break;

                case EventType.Disconnect:
                    HandleDisconnect("Disconnected by server");
                    break;

                case EventType.Timeout:
                    HandleDisconnect("Connection timeout");
                    break;

                case EventType.Receive:
                    HandleReceive(netEvent.Packet);
                    netEvent.Packet.Dispose();
                    break;
            }
        }
    }

    private void HandleConnect()
    {
        GD.Print("Connected to server, sending authentication...");
        // Send connect packet directly - bypass _connected check since we're authenticating
        var data = PacketSerializer.Serialize(new ConnectPacket
        {
            ConnectionToken = "test-token",
            ProtocolVersion = PacketSerializer.ProtocolVersion
        });
        var enetPacket = default(Packet);
        enetPacket.Create(data, PacketFlags.Reliable);
        _peer.Send(0, ref enetPacket);
    }

    private void HandleDisconnect(string reason)
    {
        _connected = false;
        GD.Print($"Disconnected: {reason}");
        EmitSignal(SignalName.Disconnected, reason);
    }

    private void HandleReceive(Packet packet)
    {
        var data = new byte[packet.Length];
        packet.CopyTo(data);

        try
        {
            var (packetType, payload) = PacketSerializer.Peek(data);
            ProcessPacket(packetType, payload);
            EmitSignal(SignalName.PacketReceived, (int)packetType, payload.ToArray());
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"Failed to process packet: {ex.Message}");
        }
    }

    private void ProcessPacket(PacketType type, ReadOnlyMemory<byte> payload)
    {
        switch (type)
        {
            case PacketType.ConnectAck:
                var connectAck = PacketSerializer.Deserialize<ConnectAckPacket>(payload);
                PlayerId = connectAck.PlayerId;
                ServerTick = connectAck.ServerTick;
                TickRate = connectAck.TickRate;
                GD.Print($"Connected as player {PlayerId}");
                break;

            case PacketType.AuthResponse:
                var authResponse = PacketSerializer.Deserialize<AuthResponsePacket>(payload);
                if (authResponse.Success)
                {
                    _connected = true;
                    GD.Print("Authenticated successfully");
                    EmitSignal(SignalName.Connected);
                }
                else
                {
                    GD.PrintErr($"Authentication failed: {authResponse.ErrorMessage}");
                    _peer.DisconnectNow(0);
                }
                break;

            case PacketType.Pong:
                var pong = PacketSerializer.Deserialize<PongPacket>(payload);
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Rtt = now - pong.ClientTime;
                break;
        }
    }
}
