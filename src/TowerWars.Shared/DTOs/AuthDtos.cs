namespace TowerWars.Shared.DTOs;

public sealed record RegisterRequest(
    string Username,
    string Email,
    string Password
);

public sealed record LoginRequest(
    string Username,
    string Password
);

public sealed record AuthResponse(
    bool Success,
    string? Token,
    string? RefreshToken,
    DateTime? ExpiresAt,
    UserDto? User,
    string? Error
);

public sealed record RefreshTokenRequest(
    string RefreshToken
);

public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public sealed record CharacterDto(
    Guid Id,
    string Name,
    string Class,
    int Level,
    long Experience,
    DateTime CreatedAt
);

public sealed record CreateCharacterRequest(
    string Name,
    string Class
);

public sealed record CharacterListResponse(
    CharacterDto[] Characters
);

public sealed record SelectCharacterRequest(
    Guid CharacterId
);

public sealed record SelectCharacterResponse(
    bool Success,
    string? ConnectionToken,
    string? Error
);
