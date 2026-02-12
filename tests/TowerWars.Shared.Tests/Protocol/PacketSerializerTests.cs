using FluentAssertions;
using TowerWars.Shared.Constants;
using TowerWars.Shared.Protocol;
using Xunit;

namespace TowerWars.Shared.Tests.Protocol;

public class PacketSerializerTests
{
    [Fact]
    public void Serialize_ConnectPacket_RoundTrips()
    {
        var original = new ConnectPacket
        {
            ConnectionToken = "test-token-123",
            ProtocolVersion = 1
        };

        var bytes = PacketSerializer.Serialize(original);
        var (type, payload) = PacketSerializer.Peek(bytes);
        var deserialized = PacketSerializer.Deserialize<ConnectPacket>(payload);

        type.Should().Be(PacketType.Connect);
        deserialized.ConnectionToken.Should().Be(original.ConnectionToken);
        deserialized.ProtocolVersion.Should().Be(original.ProtocolVersion);
    }

    [Fact]
    public void Serialize_PlayerInputPacket_RoundTrips()
    {
        var original = new PlayerInputPacket
        {
            InputSequence = 42,
            Tick = 100,
            Flags = InputFlags.MoveUp | InputFlags.PrimaryAction,
            MouseX = 123.5f,
            MouseY = 456.7f
        };

        var bytes = PacketSerializer.Serialize(original);
        var (type, payload) = PacketSerializer.Peek(bytes);
        var deserialized = PacketSerializer.Deserialize<PlayerInputPacket>(payload);

        type.Should().Be(PacketType.PlayerInput);
        deserialized.InputSequence.Should().Be(original.InputSequence);
        deserialized.Tick.Should().Be(original.Tick);
        deserialized.Flags.Should().Be(original.Flags);
        deserialized.MouseX.Should().Be(original.MouseX);
        deserialized.MouseY.Should().Be(original.MouseY);
    }

    [Fact]
    public void Serialize_StateSnapshotPacket_WithEntities_RoundTrips()
    {
        var original = new StateSnapshotPacket
        {
            Tick = 500,
            Entities = new[]
            {
                new EntityState
                {
                    EntityId = 1,
                    Type = EntityType.Tower,
                    X = 100f,
                    Y = 200f,
                    Rotation = 45f,
                    Health = 100,
                    MaxHealth = 100,
                    OwnerId = 1
                },
                new EntityState
                {
                    EntityId = 2,
                    Type = EntityType.Unit,
                    X = 300f,
                    Y = 400f,
                    Rotation = 0f,
                    Health = 50,
                    MaxHealth = 100
                }
            },
            Players = new[]
            {
                new PlayerState
                {
                    PlayerId = 1,
                    Name = "TestPlayer",
                    Gold = 500,
                    Lives = 20,
                    Score = 1000,
                    TeamId = 0,
                    IsReady = true,
                    IsConnected = true
                }
            }
        };

        var bytes = PacketSerializer.Serialize(original);
        var (type, payload) = PacketSerializer.Peek(bytes);
        var deserialized = PacketSerializer.Deserialize<StateSnapshotPacket>(payload);

        type.Should().Be(PacketType.StateSnapshot);
        deserialized.Tick.Should().Be(original.Tick);
        deserialized.Entities.Should().HaveCount(2);
        deserialized.Players.Should().HaveCount(1);
        deserialized.Entities[0].EntityId.Should().Be(1);
        deserialized.Entities[0].Type.Should().Be(EntityType.Tower);
        deserialized.Players[0].Gold.Should().Be(500);
    }

    [Fact]
    public void Serialize_TowerBuildPacket_RoundTrips()
    {
        var original = new TowerBuildPacket
        {
            RequestId = 123,
            TowerType = TowerType.Cannon,
            GridX = 5,
            GridY = 10,
            PlayerTowerId = Guid.NewGuid()
        };

        var bytes = PacketSerializer.Serialize(original);
        var (type, payload) = PacketSerializer.Peek(bytes);
        var deserialized = PacketSerializer.Deserialize<TowerBuildPacket>(payload);

        type.Should().Be(PacketType.TowerBuild);
        deserialized.RequestId.Should().Be(original.RequestId);
        deserialized.TowerType.Should().Be(original.TowerType);
        deserialized.GridX.Should().Be(original.GridX);
        deserialized.GridY.Should().Be(original.GridY);
    }

    [Fact]
    public void Peek_EmptyData_Throws()
    {
        var action = () => PacketSerializer.Peek(Array.Empty<byte>());
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deserialize_GenericMethod_WorksCorrectly()
    {
        var original = new PingPacket { ClientTime = 123456789 };
        var bytes = PacketSerializer.Serialize(original);
        var (type, payload) = PacketSerializer.Peek(bytes);

        var deserialized = PacketSerializer.Deserialize(type, payload);

        deserialized.Should().BeOfType<PingPacket>();
        ((PingPacket)deserialized).ClientTime.Should().Be(original.ClientTime);
    }
}
