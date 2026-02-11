using Microsoft.EntityFrameworkCore;
using TowerWars.Shared.DTOs;
using TowerWars.Social.Data;

namespace TowerWars.Social.Services;

public interface IFriendService
{
    Task<FriendListResponse> GetFriendsAsync(Guid userId);
    Task<bool> SendFriendRequestAsync(Guid userId, Guid friendId);
    Task<bool> AcceptFriendRequestAsync(Guid userId, Guid friendId);
    Task<bool> DeclineFriendRequestAsync(Guid userId, Guid friendId);
    Task<bool> RemoveFriendAsync(Guid userId, Guid friendId);
    Task<bool> BlockUserAsync(Guid userId, Guid blockedId);
}

public sealed class FriendService : IFriendService
{
    private readonly SocialDbContext _db;
    private readonly IPresenceService _presence;

    public FriendService(SocialDbContext db, IPresenceService presence)
    {
        _db = db;
        _presence = presence;
    }

    public async Task<FriendListResponse> GetFriendsAsync(Guid userId)
    {
        var friendships = await _db.Friends
            .Where(f => f.UserId == userId || f.FriendId == userId)
            .ToListAsync();

        var friends = new List<FriendDto>();
        var pendingRequests = new List<FriendDto>();
        var sentRequests = new List<FriendDto>();

        var friendIds = friendships
            .Select(f => f.UserId == userId ? f.FriendId : f.UserId)
            .Distinct()
            .ToList();

        var presences = await _presence.GetPresencesAsync(friendIds);

        foreach (var friendship in friendships)
        {
            var friendId = friendship.UserId == userId ? friendship.FriendId : friendship.UserId;
            var presence = presences.GetValueOrDefault(friendId, PresenceStatus.Offline);

            var dto = new FriendDto(
                friendId,
                $"User_{friendId:N}".Substring(0, 16),
                Enum.Parse<FriendStatus>(friendship.Status, true),
                presence,
                null
            );

            if (friendship.Status == "accepted")
            {
                friends.Add(dto);
            }
            else if (friendship.Status == "pending")
            {
                if (friendship.FriendId == userId)
                    pendingRequests.Add(dto);
                else
                    sentRequests.Add(dto);
            }
        }

        return new FriendListResponse(
            friends.ToArray(),
            pendingRequests.ToArray(),
            sentRequests.ToArray()
        );
    }

    public async Task<bool> SendFriendRequestAsync(Guid userId, Guid friendId)
    {
        if (userId == friendId) return false;

        var existing = await _db.Friends
            .FirstOrDefaultAsync(f =>
                (f.UserId == userId && f.FriendId == friendId) ||
                (f.UserId == friendId && f.FriendId == userId));

        if (existing != null) return false;

        _db.Friends.Add(new Friend
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FriendId = friendId,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AcceptFriendRequestAsync(Guid userId, Guid friendId)
    {
        var request = await _db.Friends
            .FirstOrDefaultAsync(f =>
                f.UserId == friendId &&
                f.FriendId == userId &&
                f.Status == "pending");

        if (request == null) return false;

        request.Status = "accepted";
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeclineFriendRequestAsync(Guid userId, Guid friendId)
    {
        var request = await _db.Friends
            .FirstOrDefaultAsync(f =>
                f.UserId == friendId &&
                f.FriendId == userId &&
                f.Status == "pending");

        if (request == null) return false;

        _db.Friends.Remove(request);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFriendAsync(Guid userId, Guid friendId)
    {
        var friendship = await _db.Friends
            .FirstOrDefaultAsync(f =>
                ((f.UserId == userId && f.FriendId == friendId) ||
                 (f.UserId == friendId && f.FriendId == userId)) &&
                f.Status == "accepted");

        if (friendship == null) return false;

        _db.Friends.Remove(friendship);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> BlockUserAsync(Guid userId, Guid blockedId)
    {
        var existing = await _db.Friends
            .FirstOrDefaultAsync(f =>
                (f.UserId == userId && f.FriendId == blockedId) ||
                (f.UserId == blockedId && f.FriendId == userId));

        if (existing != null)
        {
            existing.Status = "blocked";
            existing.UserId = userId;
            existing.FriendId = blockedId;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Friends.Add(new Friend
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FriendId = blockedId,
                Status = "blocked",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return true;
    }
}
