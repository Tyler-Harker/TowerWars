using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TowerWars.Shared.DTOs;
using TowerWars.Social.Services;

namespace TowerWars.Social.Hubs;

[Authorize]
public sealed class SocialHub : Hub
{
    private readonly IPresenceService _presence;
    private readonly IFriendService _friends;
    private readonly IPartyService _parties;
    private readonly ILogger<SocialHub> _logger;

    public SocialHub(
        IPresenceService presence,
        IFriendService friends,
        IPartyService parties,
        ILogger<SocialHub> logger)
    {
        _presence = presence;
        _friends = friends;
        _parties = parties;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            await _presence.SetPresenceAsync(userId.Value, PresenceStatus.Online);

            var friends = await _friends.GetFriendsAsync(userId.Value);
            foreach (var friend in friends.Friends)
            {
                await Clients.Group($"user:{friend.UserId}")
                    .SendAsync("FriendOnline", userId.Value);
            }

            _logger.LogDebug("User {UserId} connected", userId.Value);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await _presence.SetPresenceAsync(userId.Value, PresenceStatus.Offline);

            var friends = await _friends.GetFriendsAsync(userId.Value);
            foreach (var friend in friends.Friends)
            {
                await Clients.Group($"user:{friend.UserId}")
                    .SendAsync("FriendOffline", userId.Value);
            }

            _logger.LogDebug("User {UserId} disconnected", userId.Value);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string channel, string message, Guid? recipientId = null)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var chatMessage = new ChatMessageDto(
            Guid.NewGuid(),
            userId.Value,
            "User",
            message,
            Enum.TryParse<ChatChannelType>(channel, true, out var channelType)
                ? channelType
                : ChatChannelType.Global,
            DateTime.UtcNow
        );

        switch (channel.ToLowerInvariant())
        {
            case "global":
                await Clients.All.SendAsync("ReceiveMessage", chatMessage);
                break;

            case "party":
                var party = await _parties.GetUserPartyAsync(userId.Value);
                if (party != null)
                {
                    foreach (var member in party.Members)
                    {
                        await Clients.Group($"user:{member.UserId}")
                            .SendAsync("ReceiveMessage", chatMessage);
                    }
                }
                break;

            case "whisper":
                if (recipientId.HasValue)
                {
                    await Clients.Group($"user:{recipientId}")
                        .SendAsync("ReceiveMessage", chatMessage);
                    await Clients.Caller.SendAsync("ReceiveMessage", chatMessage);
                }
                break;
        }
    }

    public async Task SetPresence(string status)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        if (Enum.TryParse<PresenceStatus>(status, true, out var presenceStatus))
        {
            await _presence.SetPresenceAsync(userId.Value, presenceStatus);

            var friends = await _friends.GetFriendsAsync(userId.Value);
            foreach (var friend in friends.Friends)
            {
                await Clients.Group($"user:{friend.UserId}")
                    .SendAsync("FriendPresenceChanged", userId.Value, status);
            }
        }
    }

    public async Task SetPartyReady(bool isReady)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        await _parties.SetReadyAsync(userId.Value, isReady);

        var party = await _parties.GetUserPartyAsync(userId.Value);
        if (party != null)
        {
            foreach (var member in party.Members)
            {
                await Clients.Group($"user:{member.UserId}")
                    .SendAsync("PartyMemberReadyChanged", userId.Value, isReady);
            }
        }
    }

    private Guid? GetUserId()
    {
        var claim = Context.User?.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}
