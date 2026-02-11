using TowerWars.Shared.Protocol;

namespace TowerWars.Shared.DTOs;

public sealed record MatchmakingRequest(
    GameMode Mode,
    Guid? PartyId = null
);

public sealed record MatchmakingResponse(
    bool Success,
    Guid? TicketId,
    int? EstimatedWaitSeconds,
    string? Error
);

public sealed record MatchmakingStatusResponse(
    Guid TicketId,
    MatchmakingStatus Status,
    int? Position,
    int? EstimatedWaitSeconds,
    MatchFoundInfo? Match
);

public enum MatchmakingStatus
{
    Queued,
    Matching,
    Found,
    Cancelled,
    Expired
}

public sealed record MatchFoundInfo(
    Guid MatchId,
    string ZoneServerAddress,
    int ZoneServerPort,
    string ConnectionToken
);

public sealed record CancelMatchmakingRequest(
    Guid TicketId
);

public sealed record ZoneAssignment(
    string ZoneId,
    string Address,
    int Port,
    string ConnectionToken,
    DateTime ExpiresAt
);

public sealed record MatchHistoryRequest(
    int Page = 0,
    int PageSize = 20
);

public sealed record MatchHistoryResponse(
    MatchSummary[] Matches,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record MatchSummary(
    Guid MatchId,
    GameMode Mode,
    MatchResult Result,
    int WavesCompleted,
    float DurationSeconds,
    DateTime EndedAt,
    MatchParticipant[] Participants
);

public sealed record MatchParticipant(
    Guid UserId,
    string Username,
    int Score,
    int UnitsKilled,
    int TowersBuilt
);
