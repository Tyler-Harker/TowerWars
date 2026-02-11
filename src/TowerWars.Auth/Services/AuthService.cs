using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TowerWars.Auth.Data;
using TowerWars.Auth.Models;
using TowerWars.Shared.DTOs;

namespace TowerWars.Auth.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? ipAddress = null);
    Task<bool> LogoutAsync(Guid userId);
    Task<bool> ValidateSessionAsync(string tokenHash);
}

public sealed class AuthService : IAuthService
{
    private readonly AuthDbContext _db;
    private readonly IJwtService _jwt;
    private readonly ISessionCacheService _sessionCache;
    private readonly ITowerProgressionService _towerProgressionService;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        AuthDbContext db,
        IJwtService jwt,
        ISessionCacheService sessionCache,
        ITowerProgressionService towerProgressionService,
        JwtSettings jwtSettings)
    {
        _db = db;
        _jwt = jwt;
        _sessionCache = sessionCache;
        _towerProgressionService = towerProgressionService;
        _jwtSettings = jwtSettings;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3 || request.Username.Length > 32)
            return new AuthResponse(false, null, null, null, null, "Username must be between 3 and 32 characters");

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return new AuthResponse(false, null, null, null, null, "Invalid email address");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return new AuthResponse(false, null, null, null, null, "Password must be at least 8 characters");

        var existingUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Email);

        if (existingUser != null)
        {
            if (existingUser.Username == request.Username)
                return new AuthResponse(false, null, null, null, null, "Username already taken");
            return new AuthResponse(false, null, null, null, null, "Email already registered");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        var stats = new PlayerStats
        {
            UserId = user.Id,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        _db.PlayerStats.Add(stats);
        await _db.SaveChangesAsync();

        // Unlock the basic tower for new users
        await _towerProgressionService.EnsureBasicTowerUnlockedAsync(user.Id);

        return await CreateSessionAsync(user, ipAddress);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return new AuthResponse(false, null, null, null, null, "Invalid username or password");

        if (user.BannedUntil.HasValue && user.BannedUntil > DateTime.UtcNow)
            return new AuthResponse(false, null, null, null, null, $"Account banned until {user.BannedUntil:g}. Reason: {user.BanReason}");

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await CreateSessionAsync(user, ipAddress);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? ipAddress = null)
    {
        var refreshTokenHash = _jwt.HashToken(refreshToken);

        var session = await _db.Sessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash && s.ExpiresAt > DateTime.UtcNow);

        if (session?.User == null)
            return new AuthResponse(false, null, null, null, null, "Invalid or expired refresh token");

        _db.Sessions.Remove(session);
        await _db.SaveChangesAsync();

        return await CreateSessionAsync(session.User, ipAddress);
    }

    public async Task<bool> LogoutAsync(Guid userId)
    {
        var sessions = await _db.Sessions
            .Where(s => s.UserId == userId)
            .ToListAsync();

        foreach (var session in sessions)
        {
            await _sessionCache.InvalidateAsync(session.TokenHash);
        }

        _db.Sessions.RemoveRange(sessions);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ValidateSessionAsync(string tokenHash)
    {
        var cached = await _sessionCache.GetAsync(tokenHash);
        if (cached.HasValue)
            return true;

        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash && s.ExpiresAt > DateTime.UtcNow);

        if (session == null)
            return false;

        await _sessionCache.SetAsync(tokenHash, session.UserId, session.ExpiresAt - DateTime.UtcNow);
        return true;
    }

    private async Task<AuthResponse> CreateSessionAsync(User user, string? ipAddress)
    {
        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Username);
        var refreshToken = _jwt.GenerateRefreshToken();
        var accessTokenHash = _jwt.HashToken(accessToken);
        var refreshTokenHash = _jwt.HashToken(refreshToken);
        var expiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = accessTokenHash,
            RefreshTokenHash = refreshTokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            IpAddress = ipAddress
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        var accessExpiry = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
        await _sessionCache.SetAsync(accessTokenHash, user.Id, TimeSpan.FromMinutes(_jwtSettings.AccessTokenExpirationMinutes));

        var userDto = new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.CreatedAt,
            user.LastLoginAt
        );

        return new AuthResponse(true, accessToken, refreshToken, accessExpiry, userDto, null);
    }
}
