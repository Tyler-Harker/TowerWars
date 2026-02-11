using Microsoft.EntityFrameworkCore;
using TowerWars.Shared.DTOs;
using TowerWars.Social.Data;
using DataPartyMember = TowerWars.Social.Data.PartyMember;

namespace TowerWars.Social.Services;

public interface IPartyService
{
    Task<PartyDto?> GetPartyAsync(Guid partyId);
    Task<PartyDto?> GetUserPartyAsync(Guid userId);
    Task<PartyDto?> CreatePartyAsync(Guid leaderId);
    Task<bool> JoinPartyAsync(Guid partyId, Guid userId);
    Task<bool> LeavePartyAsync(Guid userId);
    Task<bool> KickMemberAsync(Guid leaderId, Guid userId);
    Task<bool> SetReadyAsync(Guid userId, bool isReady);
    Task<bool> TransferLeadershipAsync(Guid leaderId, Guid newLeaderId);
    Task<bool> DisbandPartyAsync(Guid leaderId);
}

public sealed class PartyService : IPartyService
{
    private readonly SocialDbContext _db;
    private readonly IPresenceService _presence;

    public PartyService(SocialDbContext db, IPresenceService presence)
    {
        _db = db;
        _presence = presence;
    }

    public async Task<PartyDto?> GetPartyAsync(Guid partyId)
    {
        var party = await _db.Parties
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == partyId);

        if (party == null) return null;

        return await MapToDto(party);
    }

    public async Task<PartyDto?> GetUserPartyAsync(Guid userId)
    {
        var membership = await _db.PartyMembers
            .Include(m => m.Party)
            .ThenInclude(p => p!.Members)
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (membership?.Party == null) return null;

        return await MapToDto(membership.Party);
    }

    public async Task<PartyDto?> CreatePartyAsync(Guid leaderId)
    {
        var existingMembership = await _db.PartyMembers
            .FirstOrDefaultAsync(m => m.UserId == leaderId);

        if (existingMembership != null) return null;

        var party = new Party
        {
            Id = Guid.NewGuid(),
            LeaderId = leaderId,
            MaxSize = 6,
            CreatedAt = DateTime.UtcNow
        };

        var member = new DataPartyMember
        {
            PartyId = party.Id,
            UserId = leaderId,
            IsReady = false,
            JoinedAt = DateTime.UtcNow
        };

        _db.Parties.Add(party);
        _db.PartyMembers.Add(member);
        await _db.SaveChangesAsync();

        party.Members = [member];
        return await MapToDto(party);
    }

    public async Task<bool> JoinPartyAsync(Guid partyId, Guid userId)
    {
        var party = await _db.Parties
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == partyId);

        if (party == null) return false;
        if (party.Members.Count >= party.MaxSize) return false;

        var existingMembership = await _db.PartyMembers
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (existingMembership != null) return false;

        _db.PartyMembers.Add(new DataPartyMember
        {
            PartyId = partyId,
            UserId = userId,
            IsReady = false,
            JoinedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LeavePartyAsync(Guid userId)
    {
        var membership = await _db.PartyMembers
            .Include(m => m.Party)
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (membership?.Party == null) return false;

        var party = membership.Party;

        if (party.LeaderId == userId)
        {
            var otherMembers = await _db.PartyMembers
                .Where(m => m.PartyId == party.Id && m.UserId != userId)
                .OrderBy(m => m.JoinedAt)
                .FirstOrDefaultAsync();

            if (otherMembers != null)
            {
                party.LeaderId = otherMembers.UserId;
            }
            else
            {
                _db.Parties.Remove(party);
            }
        }

        _db.PartyMembers.Remove(membership);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> KickMemberAsync(Guid leaderId, Guid userId)
    {
        var party = await _db.Parties
            .FirstOrDefaultAsync(p => p.LeaderId == leaderId);

        if (party == null) return false;

        var membership = await _db.PartyMembers
            .FirstOrDefaultAsync(m => m.PartyId == party.Id && m.UserId == userId);

        if (membership == null) return false;

        _db.PartyMembers.Remove(membership);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetReadyAsync(Guid userId, bool isReady)
    {
        var membership = await _db.PartyMembers
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (membership == null) return false;

        membership.IsReady = isReady;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> TransferLeadershipAsync(Guid leaderId, Guid newLeaderId)
    {
        var party = await _db.Parties
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.LeaderId == leaderId);

        if (party == null) return false;
        if (!party.Members.Any(m => m.UserId == newLeaderId)) return false;

        party.LeaderId = newLeaderId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DisbandPartyAsync(Guid leaderId)
    {
        var party = await _db.Parties
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.LeaderId == leaderId);

        if (party == null) return false;

        _db.PartyMembers.RemoveRange(party.Members);
        _db.Parties.Remove(party);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<PartyDto> MapToDto(Party party)
    {
        var memberIds = party.Members.Select(m => m.UserId).ToList();
        var presences = await _presence.GetPresencesAsync(memberIds);

        var members = party.Members.Select(m => new Shared.DTOs.PartyMember(
            m.UserId,
            $"User_{m.UserId:N}".Substring(0, 16),
            presences.GetValueOrDefault(m.UserId, PresenceStatus.Offline),
            m.IsReady
        )).ToArray();

        return new PartyDto(
            party.Id,
            party.LeaderId,
            members,
            party.MaxSize
        );
    }
}
