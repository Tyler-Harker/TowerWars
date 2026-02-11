namespace TowerWars.Shared.DTOs;

public sealed record FriendDto(
    Guid UserId,
    string Username,
    FriendStatus Status,
    PresenceStatus Presence,
    DateTime? LastOnline
);

public enum FriendStatus
{
    Pending,
    Accepted,
    Blocked
}

public enum PresenceStatus
{
    Offline,
    Online,
    InGame,
    Away
}

public sealed record FriendListResponse(
    FriendDto[] Friends,
    FriendDto[] PendingRequests,
    FriendDto[] SentRequests
);

public sealed record SendFriendRequestRequest(
    string Username
);

public sealed record FriendRequestResponse(
    bool Success,
    string? Error
);

public sealed record RespondToFriendRequestRequest(
    Guid UserId,
    bool Accept
);

public sealed record PartyDto(
    Guid PartyId,
    Guid LeaderId,
    PartyMember[] Members,
    int MaxSize
);

public sealed record PartyMember(
    Guid UserId,
    string Username,
    PresenceStatus Presence,
    bool IsReady
);

public sealed record CreatePartyResponse(
    bool Success,
    Guid? PartyId,
    string? Error
);

public sealed record InviteToPartyRequest(
    string Username
);

public sealed record JoinPartyRequest(
    Guid PartyId
);

public sealed record PartyResponse(
    bool Success,
    PartyDto? Party,
    string? Error
);

public sealed record ChatMessageDto(
    Guid MessageId,
    Guid SenderId,
    string SenderName,
    string Content,
    ChatChannelType Channel,
    DateTime SentAt
);

public enum ChatChannelType
{
    Global,
    Party,
    Whisper,
    Game
}

public sealed record SendChatMessageRequest(
    ChatChannelType Channel,
    string Content,
    Guid? RecipientId = null
);

public sealed record PlayerStatsDto(
    Guid UserId,
    string Username,
    int Wins,
    int Losses,
    int EloRating,
    int HighestWaveSolo,
    int TotalUnitsKilled,
    int TotalTowersBuilt,
    int TotalGoldEarned,
    TimeSpan TotalPlayTime
);

public sealed record LeaderboardEntry(
    int Rank,
    Guid UserId,
    string Username,
    int Score,
    int Wins,
    int EloRating
);

public sealed record LeaderboardResponse(
    LeaderboardType Type,
    LeaderboardEntry[] Entries,
    int TotalCount,
    LeaderboardEntry? PlayerEntry
);

public enum LeaderboardType
{
    Elo,
    HighestWave,
    TotalWins,
    TotalKills
}
