using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TowerWars.Auth.Data;
using TowerWars.Auth.Models;
using TowerWars.Shared.Constants;
using TowerWars.Shared.DTOs;

namespace TowerWars.Auth.Services;

public interface ITowerProgressionService
{
    Task<List<PlayerTowerDto>> GetPlayerTowersAsync(Guid userId);
    Task<PlayerTowerDetailDto?> GetTowerDetailAsync(Guid userId, byte towerType);
    Task<AllocateSkillResponse> AllocateSkillPointAsync(Guid userId, AllocateSkillRequest request);
    Task<ResetSkillsResponse> ResetSkillsAsync(Guid userId, ResetSkillsRequest request);
    Task<TowerUnlockResponse> UnlockTowerAsync(Guid userId, TowerUnlockRequest request);
    Task<TowerBonusSummaryDto> GetTowerBonusesAsync(Guid userId, byte towerType);
    Task AddTowerXpAsync(Guid userId, byte towerType, int xpAmount);
    Task EnsureBasicTowerUnlockedAsync(Guid userId);
}

public class TowerProgressionService : ITowerProgressionService
{
    private readonly AuthDbContext _db;

    public TowerProgressionService(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<List<PlayerTowerDto>> GetPlayerTowersAsync(Guid userId)
    {
        var towers = await _db.PlayerTowers
            .Where(pt => pt.UserId == userId)
            .Include(pt => pt.AllocatedSkills)
            .ToListAsync();

        return towers.Select(t =>
        {
            var totalSkillPoints = TowerProgressionConstants.GetTotalSkillPointsForLevel(t.Level);
            var usedSkillPoints = t.AllocatedSkills.Sum(s => s.RanksAllocated * GetSkillCost(s.SkillNodeId));

            return new PlayerTowerDto(
                t.Id,
                (byte)t.TowerType,
                t.TowerType.ToString(),
                t.Experience,
                t.Level,
                totalSkillPoints - usedSkillPoints,
                t.Unlocked,
                t.UnlockedAt
            );
        }).ToList();
    }

    public async Task<PlayerTowerDetailDto?> GetTowerDetailAsync(Guid userId, byte towerType)
    {
        var tower = await _db.PlayerTowers
            .Where(pt => pt.UserId == userId && pt.TowerType == (TowerType)towerType)
            .Include(pt => pt.AllocatedSkills)
                .ThenInclude(s => s.SkillNode)
            .FirstOrDefaultAsync();

        if (tower == null) return null;

        var skillNodes = await _db.TowerSkillNodes
            .Where(sn => sn.TowerType == (TowerType)towerType)
            .ToListAsync();

        var totalSkillPoints = TowerProgressionConstants.GetTotalSkillPointsForLevel(tower.Level);
        var usedSkillPoints = tower.AllocatedSkills.Sum(s => s.RanksAllocated * s.SkillNode!.SkillPointsCost);

        var (currentXp, requiredXp) = TowerProgressionConstants.GetLevelProgress(tower.Experience);

        return new PlayerTowerDetailDto(
            tower.Id,
            towerType,
            tower.TowerType.ToString(),
            tower.Experience,
            requiredXp,
            tower.Level,
            TowerProgressionConstants.MaxLevel,
            totalSkillPoints - usedSkillPoints,
            totalSkillPoints,
            tower.Unlocked,
            tower.UnlockedAt,
            skillNodes.Select(ToSkillNodeDto).ToList(),
            tower.AllocatedSkills.Select(s => new AllocatedSkillDto(
                s.SkillNodeId,
                s.SkillNode!.NodeId,
                s.RanksAllocated,
                s.AllocatedAt
            )).ToList(),
            CalculateBonusSummary(tower.AllocatedSkills)
        );
    }

    public async Task<AllocateSkillResponse> AllocateSkillPointAsync(Guid userId, AllocateSkillRequest request)
    {
        var tower = await _db.PlayerTowers
            .Where(pt => pt.Id == request.PlayerTowerId && pt.UserId == userId)
            .Include(pt => pt.AllocatedSkills)
                .ThenInclude(s => s.SkillNode)
            .FirstOrDefaultAsync();

        if (tower == null)
            return new AllocateSkillResponse(false, "Tower not found", 0, null);

        if (!tower.Unlocked)
            return new AllocateSkillResponse(false, "Tower is not unlocked", 0, null);

        var skillNode = await _db.TowerSkillNodes.FindAsync(request.SkillNodeId);
        if (skillNode == null)
            return new AllocateSkillResponse(false, "Skill node not found", 0, null);

        if (skillNode.TowerType != tower.TowerType)
            return new AllocateSkillResponse(false, "Skill node does not belong to this tower type", 0, null);

        if (tower.Level < skillNode.RequiredTowerLevel)
            return new AllocateSkillResponse(false, $"Tower level {skillNode.RequiredTowerLevel} required", 0, null);

        // Check prerequisites
        foreach (var prereqNodeId in skillNode.PrerequisiteNodeIds)
        {
            var prereqAllocated = tower.AllocatedSkills.Any(s => s.SkillNode!.NodeId == prereqNodeId);
            if (!prereqAllocated)
                return new AllocateSkillResponse(false, $"Prerequisite skill '{prereqNodeId}' not allocated", 0, null);
        }

        // Check available skill points
        var totalSkillPoints = TowerProgressionConstants.GetTotalSkillPointsForLevel(tower.Level);
        var usedSkillPoints = tower.AllocatedSkills.Sum(s => s.RanksAllocated * s.SkillNode!.SkillPointsCost);
        var availablePoints = totalSkillPoints - usedSkillPoints;
        var pointsNeeded = request.RanksToAllocate * skillNode.SkillPointsCost;

        if (availablePoints < pointsNeeded)
            return new AllocateSkillResponse(false, "Not enough skill points", availablePoints, null);

        // Check existing allocation
        var existingAllocation = tower.AllocatedSkills
            .FirstOrDefault(s => s.SkillNodeId == request.SkillNodeId);

        int currentRanks = existingAllocation?.RanksAllocated ?? 0;
        int newRanks = currentRanks + request.RanksToAllocate;

        if (newRanks > skillNode.MaxRanks)
            return new AllocateSkillResponse(false, $"Max ranks ({skillNode.MaxRanks}) already allocated", availablePoints, null);

        if (existingAllocation != null)
        {
            existingAllocation.RanksAllocated = (short)newRanks;
        }
        else
        {
            existingAllocation = new PlayerTowerSkill
            {
                Id = Guid.NewGuid(),
                PlayerTowerId = tower.Id,
                SkillNodeId = request.SkillNodeId,
                RanksAllocated = (short)request.RanksToAllocate,
                AllocatedAt = DateTime.UtcNow,
                SkillNode = skillNode
            };
            _db.PlayerTowerSkills.Add(existingAllocation);
        }

        tower.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AllocateSkillResponse(
            true,
            null,
            availablePoints - pointsNeeded,
            new AllocatedSkillDto(
                existingAllocation.SkillNodeId,
                skillNode.NodeId,
                existingAllocation.RanksAllocated,
                existingAllocation.AllocatedAt
            )
        );
    }

    public async Task<ResetSkillsResponse> ResetSkillsAsync(Guid userId, ResetSkillsRequest request)
    {
        var tower = await _db.PlayerTowers
            .Where(pt => pt.Id == request.PlayerTowerId && pt.UserId == userId)
            .Include(pt => pt.AllocatedSkills)
                .ThenInclude(s => s.SkillNode)
            .FirstOrDefaultAsync();

        if (tower == null)
            return new ResetSkillsResponse(false, "Tower not found", 0);

        var refundedPoints = tower.AllocatedSkills.Sum(s => s.RanksAllocated * s.SkillNode!.SkillPointsCost);

        _db.PlayerTowerSkills.RemoveRange(tower.AllocatedSkills);
        tower.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new ResetSkillsResponse(true, null, refundedPoints);
    }

    public async Task<TowerUnlockResponse> UnlockTowerAsync(Guid userId, TowerUnlockRequest request)
    {
        var towerType = (TowerType)request.TowerType;

        var existing = await _db.PlayerTowers
            .FirstOrDefaultAsync(pt => pt.UserId == userId && pt.TowerType == towerType);

        if (existing != null && existing.Unlocked)
            return new TowerUnlockResponse(false, "Tower already unlocked", null);

        if (existing != null)
        {
            existing.Unlocked = true;
            existing.UnlockedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new PlayerTower
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TowerType = towerType,
                Experience = 0,
                Level = 1,
                Unlocked = true,
                UnlockedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.PlayerTowers.Add(existing);
        }

        await _db.SaveChangesAsync();

        return new TowerUnlockResponse(
            true,
            null,
            new PlayerTowerDto(
                existing.Id,
                request.TowerType,
                towerType.ToString(),
                existing.Experience,
                existing.Level,
                TowerProgressionConstants.GetTotalSkillPointsForLevel(existing.Level),
                existing.Unlocked,
                existing.UnlockedAt
            )
        );
    }

    public async Task<TowerBonusSummaryDto> GetTowerBonusesAsync(Guid userId, byte towerType)
    {
        var tower = await _db.PlayerTowers
            .Where(pt => pt.UserId == userId && pt.TowerType == (TowerType)towerType)
            .Include(pt => pt.AllocatedSkills)
                .ThenInclude(s => s.SkillNode)
            .FirstOrDefaultAsync();

        if (tower == null)
            return CreateEmptyBonusSummary();

        return CalculateBonusSummary(tower.AllocatedSkills);
    }

    public async Task AddTowerXpAsync(Guid userId, byte towerType, int xpAmount)
    {
        var tower = await _db.PlayerTowers
            .FirstOrDefaultAsync(pt => pt.UserId == userId && pt.TowerType == (TowerType)towerType);

        if (tower == null || !tower.Unlocked) return;

        tower.Experience += xpAmount;
        var newLevel = TowerProgressionConstants.GetLevelFromXp(tower.Experience);

        if (newLevel > tower.Level)
        {
            tower.Level = newLevel;
        }

        tower.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task EnsureBasicTowerUnlockedAsync(Guid userId)
    {
        var basicTower = await _db.PlayerTowers
            .FirstOrDefaultAsync(pt => pt.UserId == userId && pt.TowerType == TowerType.Basic);

        if (basicTower == null)
        {
            basicTower = new PlayerTower
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TowerType = TowerType.Basic,
                Experience = 0,
                Level = 1,
                Unlocked = true,
                UnlockedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.PlayerTowers.Add(basicTower);
            await _db.SaveChangesAsync();
        }
        else if (!basicTower.Unlocked)
        {
            basicTower.Unlocked = true;
            basicTower.UnlockedAt = DateTime.UtcNow;
            basicTower.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    private int GetSkillCost(Guid skillNodeId)
    {
        var node = _db.TowerSkillNodes.Find(skillNodeId);
        return node?.SkillPointsCost ?? 1;
    }

    private static SkillNodeDto ToSkillNodeDto(TowerSkillNode node) => new(
        node.Id,
        node.NodeId,
        node.Tier,
        node.PositionX,
        node.PositionY,
        node.Name,
        node.Description,
        node.SkillPointsCost,
        node.RequiredTowerLevel,
        node.BonusType,
        node.BonusValue,
        node.BonusValuePerRank,
        node.MaxRanks,
        node.PrerequisiteNodeIds
    );

    private static TowerBonusSummaryDto CalculateBonusSummary(IEnumerable<PlayerTowerSkill> skills)
    {
        var bonuses = new Dictionary<TowerBonusType, decimal>();

        foreach (var skill in skills)
        {
            if (skill.SkillNode == null) continue;

            var totalValue = skill.SkillNode.BonusValue +
                (skill.RanksAllocated - 1) * skill.SkillNode.BonusValuePerRank;

            if (bonuses.ContainsKey(skill.SkillNode.BonusType))
                bonuses[skill.SkillNode.BonusType] += totalValue;
            else
                bonuses[skill.SkillNode.BonusType] = totalValue;
        }

        return new TowerBonusSummaryDto(
            bonuses.GetValueOrDefault(TowerBonusType.DamagePercent, 0),
            bonuses.GetValueOrDefault(TowerBonusType.DamageFlat, 0),
            bonuses.GetValueOrDefault(TowerBonusType.AttackSpeedPercent, 0),
            bonuses.GetValueOrDefault(TowerBonusType.RangePercent, 0),
            bonuses.GetValueOrDefault(TowerBonusType.CritChance, 0),
            bonuses.GetValueOrDefault(TowerBonusType.CritMultiplier, 0),
            bonuses.GetValueOrDefault(TowerBonusType.TowerHpFlat, 0),
            bonuses.GetValueOrDefault(TowerBonusType.TowerHpPercent, 0),
            bonuses.GetValueOrDefault(TowerBonusType.DamageReductionPercent, 0),
            bonuses.GetValueOrDefault(TowerBonusType.GoldFindPercent, 0),
            bonuses.GetValueOrDefault(TowerBonusType.XpGainPercent, 0),
            bonuses
        );
    }

    private static TowerBonusSummaryDto CreateEmptyBonusSummary() => new(
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, new Dictionary<TowerBonusType, decimal>()
    );
}
