using Grpc.Core;
using TowerWars.Shared.Protocol;
using TowerWars.WorldManager.Protos;

namespace TowerWars.WorldManager.Services;

public sealed class WorldGrpcService : WorldService.WorldServiceBase
{
    private readonly IZoneOrchestrator _zoneOrchestrator;
    private readonly ILogger<WorldGrpcService> _logger;

    public WorldGrpcService(IZoneOrchestrator zoneOrchestrator, ILogger<WorldGrpcService> logger)
    {
        _zoneOrchestrator = zoneOrchestrator;
        _logger = logger;
    }

    public override async Task<ZoneAssignmentResponse> GetZoneAssignment(
        ZoneAssignmentRequest request,
        ServerCallContext context)
    {
        var mode = Enum.TryParse<GameMode>(request.GameMode, true, out var parsed)
            ? parsed
            : GameMode.Solo;

        var zone = await _zoneOrchestrator.GetAvailableZoneAsync(mode);

        if (zone == null)
        {
            return new ZoneAssignmentResponse
            {
                Success = false,
                Error = "No available zones"
            };
        }

        var connectionToken = Guid.NewGuid().ToString("N");

        return new ZoneAssignmentResponse
        {
            Success = true,
            ZoneId = zone.ZoneId,
            Address = zone.Address,
            Port = zone.Port,
            ConnectionToken = connectionToken
        };
    }

    public override async Task<CreateInstanceResponse> CreateInstance(
        CreateInstanceRequest request,
        ServerCallContext context)
    {
        var mode = Enum.TryParse<GameMode>(request.GameMode, true, out var parsed)
            ? parsed
            : GameMode.Solo;

        var playerIds = request.PlayerIds.Select(Guid.Parse).ToList();
        var zone = await _zoneOrchestrator.CreateInstanceAsync(mode, playerIds, request.MapId);

        if (zone == null)
        {
            return new CreateInstanceResponse
            {
                Success = false,
                Error = "Failed to create instance"
            };
        }

        return new CreateInstanceResponse
        {
            Success = true,
            InstanceId = Guid.NewGuid().ToString(),
            ZoneId = zone.ZoneId,
            Address = zone.Address,
            Port = zone.Port
        };
    }

    public override async Task<DestroyInstanceResponse> DestroyInstance(
        DestroyInstanceRequest request,
        ServerCallContext context)
    {
        var success = await _zoneOrchestrator.DestroyInstanceAsync(request.InstanceId);
        return new DestroyInstanceResponse { Success = success };
    }

    public override async Task<ZoneStatusResponse> GetZoneStatus(
        ZoneStatusRequest request,
        ServerCallContext context)
    {
        var zone = await _zoneOrchestrator.GetZoneAsync(request.ZoneId);

        if (zone == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Zone not found"));
        }

        return new ZoneStatusResponse
        {
            ZoneId = zone.ZoneId,
            Status = zone.Status.ToString(),
            PlayerCount = zone.PlayerCount,
            Capacity = zone.Capacity,
            UptimeSeconds = (long)(DateTime.UtcNow - zone.CreatedAt).TotalSeconds
        };
    }

    public override async Task<RegisterZoneResponse> RegisterZone(
        RegisterZoneRequest request,
        ServerCallContext context)
    {
        var zone = new ZoneInstance(
            request.ZoneId,
            request.Address,
            request.Port,
            0,
            request.Capacity,
            ZoneStatus.Ready,
            DateTime.UtcNow,
            DateTime.UtcNow,
            []
        );

        await _zoneOrchestrator.RegisterZoneAsync(zone);

        return new RegisterZoneResponse { Success = true };
    }

    public override async Task<ZoneHeartbeatResponse> ZoneHeartbeat(
        ZoneHeartbeatRequest request,
        ServerCallContext context)
    {
        await _zoneOrchestrator.UpdateZoneAsync(
            request.ZoneId,
            request.PlayerCount,
            request.ActiveMatches
        );

        return new ZoneHeartbeatResponse
        {
            Acknowledged = true,
            ShouldShutdown = false
        };
    }
}
