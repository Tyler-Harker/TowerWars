using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TowerWars.Shared.Protocol;
using TowerWars.ZoneServer.Game;
using TowerWars.ZoneServer.Networking;
using TowerWars.ZoneServer.Services;
using Xunit;

namespace TowerWars.ZoneServer.Tests.Game;

public class GameSessionTests
{
    private readonly Mock<ILogger<GameSession>> _loggerMock;
    private readonly Mock<ENetServer> _serverMock;
    private readonly Mock<PlayerManager> _playerManagerMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<ITowerBonusService> _towerBonusServiceMock;

    public GameSessionTests()
    {
        _loggerMock = new Mock<ILogger<GameSession>>();
        _serverMock = new Mock<ENetServer>(
            new Mock<ILogger<ENetServer>>().Object,
            new Mock<PacketRouter>(
                new Mock<ILogger<PacketRouter>>().Object,
                null!,
                null!
            ).Object
        );
        _playerManagerMock = new Mock<PlayerManager>(
            new Mock<ILogger<PlayerManager>>().Object,
            _serverMock.Object,
            new Mock<ITokenValidationService>().Object,
            new Mock<IEventPublisher>().Object
        );
        _eventPublisherMock = new Mock<IEventPublisher>();
        _towerBonusServiceMock = new Mock<ITowerBonusService>();
    }

    [Fact]
    public void GameSession_InitialState_IsWaitingForPlayers()
    {
        var session = CreateGameSession();
        session.State.Should().Be(GameState.WaitingForPlayers);
    }

    [Fact]
    public void GameSession_HasUniqueMatchId()
    {
        var session1 = CreateGameSession();
        var session2 = CreateGameSession();

        session1.MatchId.Should().NotBeEmpty();
        session2.MatchId.Should().NotBeEmpty();
        session1.MatchId.Should().NotBe(session2.MatchId);
    }

    [Fact]
    public void GameSession_InitialWave_IsZero()
    {
        var session = CreateGameSession();
        session.CurrentWave.Should().Be(0);
    }

    private GameSession CreateGameSession()
    {
        return new GameSession(
            _loggerMock.Object,
            new Lazy<ENetServer>(() => _serverMock.Object),
            _playerManagerMock.Object,
            _eventPublisherMock.Object,
            _towerBonusServiceMock.Object
        );
    }
}
