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
    private readonly GameSessionManager _sessionManager;
    private readonly ConnectionManager _connectionManager;
    private readonly ushort _port;

    private const double TickInterval = 1.0 / GameConstants.DefaultTickRate;

    public GameLoop(
        ILogger<GameLoop> logger,
        Lazy<ENetServer> server,
        GameSessionManager sessionManager,
        ConnectionManager connectionManager,
        IConfiguration configuration)
    {
        _logger = logger;
        _serverLazy = server;
        _sessionManager = sessionManager;
        _connectionManager = connectionManager;
        _port = ushort.Parse(configuration["Server:Port"] ?? "7100");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wire up events now that all dependencies are resolved
        _connectionManager.EnsureEventsWired();
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
                foreach (var session in _sessionManager.GetAllSessions())
                {
                    session.Update((float)TickInterval);
                    session.Tick();
                }
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
