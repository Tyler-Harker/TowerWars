using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TowerWars.Shared.Constants;
using TowerWars.ZoneServer.Game;
using TowerWars.ZoneServer.Networking;

namespace TowerWars.ZoneServer;

public sealed class GameLoop : BackgroundService
{
    private readonly ILogger<GameLoop> _logger;
    private readonly Lazy<ENetServer> _serverLazy;
    private ENetServer _server => _serverLazy.Value;
    private readonly GameSession _gameSession;
    private readonly PlayerManager _playerManager;
    private readonly ushort _port;

    private const double TickInterval = 1.0 / GameConstants.DefaultTickRate;

    public GameLoop(
        ILogger<GameLoop> logger,
        Lazy<ENetServer> server,
        GameSession gameSession,
        PlayerManager playerManager,
        IConfiguration configuration)
    {
        _logger = logger;
        _serverLazy = server;
        _gameSession = gameSession;
        _playerManager = playerManager;
        _port = ushort.Parse(configuration["Server:Port"] ?? "7100");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wire up events now that all dependencies are resolved
        _playerManager.EnsureEventsWired();
        _server.Start(_port);
        _logger.LogInformation("Game loop started at {TickRate} ticks/sec", GameConstants.DefaultTickRate);

        var stopwatch = Stopwatch.StartNew();
        var accumulator = 0.0;
        var lastTime = stopwatch.Elapsed.TotalSeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var deltaTime = currentTime - lastTime;
            lastTime = currentTime;

            accumulator += deltaTime;

            _server.Poll(0);

            while (accumulator >= TickInterval)
            {
                _gameSession.Update((float)TickInterval);
                _gameSession.Tick();
                accumulator -= TickInterval;
            }

            var sleepTime = Math.Max(0, (TickInterval - (stopwatch.Elapsed.TotalSeconds - currentTime)) * 1000);
            if (sleepTime > 1)
            {
                await Task.Delay((int)sleepTime, stoppingToken);
            }
        }

        _server.Stop();
        _logger.LogInformation("Game loop stopped");
    }
}
