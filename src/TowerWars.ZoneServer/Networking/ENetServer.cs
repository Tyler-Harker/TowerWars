using ENet;
using Microsoft.Extensions.Logging;
using TowerWars.Shared.Protocol;

namespace TowerWars.ZoneServer.Networking;

public class ENetServer : IDisposable
{
    private readonly ILogger<ENetServer> _logger;
    private readonly PacketRouter _packetRouter;
    private readonly Dictionary<uint, ConnectedPeer> _peers = new();
    private readonly object _peersLock = new();

    private Host _host = null!;
    private bool _running;
    private uint _nextPeerId;

    public event Action<uint>? OnPeerConnected;
    public event Action<uint, string>? OnPeerDisconnected;

    public ENetServer(ILogger<ENetServer> logger, PacketRouter packetRouter)
    {
        _logger = logger;
        _packetRouter = packetRouter;
    }

    public void Start(ushort port, int maxClients = 64)
    {
        Library.Initialize();

        _host = new Host();
        var address = new Address { Port = port };
        _host.Create(address, maxClients, 2);

        _running = true;
        _logger.LogInformation("ENet server started on port {Port}", port);
    }

    public void Poll(int timeout = 0)
    {
        if (!_running) return;

        while (_host.Service(timeout, out var netEvent) > 0)
        {
            switch (netEvent.Type)
            {
                case EventType.Connect:
                    HandleConnect(netEvent.Peer);
                    break;

                case EventType.Disconnect:
                    HandleDisconnect(netEvent.Peer, "Disconnected");
                    break;

                case EventType.Timeout:
                    HandleDisconnect(netEvent.Peer, "Timeout");
                    break;

                case EventType.Receive:
                    HandleReceive(netEvent.Peer, netEvent.Packet);
                    netEvent.Packet.Dispose();
                    break;
            }
        }
    }

    public void Send<T>(uint peerId, T packet, PacketFlags flags = PacketFlags.Reliable) where T : IPacket
    {
        lock (_peersLock)
        {
            if (!_peers.TryGetValue(peerId, out var connectedPeer))
                return;

            var data = PacketSerializer.Serialize(packet);
            var enetPacket = default(Packet);
            enetPacket.Create(data, flags);

            byte channel = flags.HasFlag(PacketFlags.Reliable) ? (byte)0 : (byte)1;
            connectedPeer.Peer.Send(channel, ref enetPacket);
        }
    }

    public void Broadcast<T>(T packet, PacketFlags flags = PacketFlags.Reliable) where T : IPacket
    {
        var data = PacketSerializer.Serialize(packet);

        lock (_peersLock)
        {
            foreach (var connectedPeer in _peers.Values)
            {
                var enetPacket = default(Packet);
                enetPacket.Create(data, flags);
                byte channel = flags.HasFlag(PacketFlags.Reliable) ? (byte)0 : (byte)1;
                connectedPeer.Peer.Send(channel, ref enetPacket);
            }
        }
    }

    public void BroadcastExcept<T>(uint excludePeerId, T packet, PacketFlags flags = PacketFlags.Reliable) where T : IPacket
    {
        var data = PacketSerializer.Serialize(packet);

        lock (_peersLock)
        {
            foreach (var (peerId, connectedPeer) in _peers)
            {
                if (peerId == excludePeerId) continue;

                var enetPacket = default(Packet);
                enetPacket.Create(data, flags);
                byte channel = flags.HasFlag(PacketFlags.Reliable) ? (byte)0 : (byte)1;
                connectedPeer.Peer.Send(channel, ref enetPacket);
            }
        }
    }

    public void Disconnect(uint peerId, string reason = "Kicked")
    {
        lock (_peersLock)
        {
            if (!_peers.TryGetValue(peerId, out var connectedPeer))
                return;

            var disconnectPacket = new DisconnectPacket { Reason = reason };
            Send(peerId, disconnectPacket);

            connectedPeer.Peer.DisconnectLater(0);
        }
    }

    public int ConnectedPeerCount
    {
        get
        {
            lock (_peersLock) return _peers.Count;
        }
    }

    public void Stop()
    {
        if (!_running) return;

        _running = false;

        lock (_peersLock)
        {
            foreach (var peer in _peers.Values)
            {
                peer.Peer.DisconnectNow(0);
            }
            _peers.Clear();
        }

        _host.Flush();
        _host.Dispose();

        Library.Deinitialize();
        _logger.LogInformation("ENet server stopped");
    }

    public void Dispose() => Stop();

    private void HandleConnect(Peer peer)
    {
        var peerId = ++_nextPeerId;

        lock (_peersLock)
        {
            _peers[peerId] = new ConnectedPeer(peerId, peer);
        }

        peer.Timeout(0, 5000, 30000);

        _logger.LogDebug("Peer {PeerId} connected from {Address}", peerId, peer.IP);
        OnPeerConnected?.Invoke(peerId);
    }

    private void HandleDisconnect(Peer peer, string reason)
    {
        uint? peerId = null;

        lock (_peersLock)
        {
            foreach (var (id, connectedPeer) in _peers)
            {
                if (connectedPeer.Peer.ID == peer.ID)
                {
                    peerId = id;
                    _peers.Remove(id);
                    break;
                }
            }
        }

        if (peerId.HasValue)
        {
            _logger.LogDebug("Peer {PeerId} disconnected: {Reason}", peerId.Value, reason);
            OnPeerDisconnected?.Invoke(peerId.Value, reason);
        }
    }

    private void HandleReceive(Peer peer, Packet packet)
    {
        uint? peerId = null;

        lock (_peersLock)
        {
            foreach (var (id, connectedPeer) in _peers)
            {
                if (connectedPeer.Peer.ID == peer.ID)
                {
                    peerId = id;
                    break;
                }
            }
        }

        if (!peerId.HasValue) return;

        var data = new byte[packet.Length];
        packet.CopyTo(data);

        try
        {
            var (packetType, payload) = PacketSerializer.Peek(data);
            _packetRouter.Route(peerId.Value, packetType, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process packet from peer {PeerId}", peerId.Value);
        }
    }

    private sealed record ConnectedPeer(uint Id, Peer Peer);
}
