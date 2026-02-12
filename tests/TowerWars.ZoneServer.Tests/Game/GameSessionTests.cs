using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TowerWars.Shared.Protocol;
using TowerWars.ZoneServer.Game;
using TowerWars.ZoneServer.Services;
using Xunit;

namespace TowerWars.ZoneServer.Tests.Game;

public class GameSessionTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<ITowerBonusService> _towerBonusServiceMock;

    public GameSessionTests()
    {
        _loggerMock = new Mock<ILogger>();
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
        Action<uint, IPacket> send = (_, _) => { };
        Action<IPacket> broadcast = _ => { };
        var playerManager = new SessionPlayerManager(send, broadcast, _loggerMock.Object);

        return new GameSession(
            Guid.NewGuid(),
            _loggerMock.Object,
            playerManager,
            _eventPublisherMock.Object,
            _towerBonusServiceMock.Object,
            send,
            broadcast
        );
    }
}
