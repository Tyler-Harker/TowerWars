using TowerWars.Shared.Constants;
using TowerWars.Shared.DTOs;

namespace TowerWars.Auth.Models;

public class User
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? BannedUntil { get; set; }
    public string? BanReason { get; set; }
    public int InventorySlots { get; set; } = TowerProgressionConstants.StartingInventorySlots;

    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<Character> Characters { get; set; } = [];
    public ICollection<PlayerTower> PlayerTowers { get; set; } = [];
    public ICollection<PlayerItem> PlayerItems { get; set; } = [];
    public PlayerStats? Stats { get; set; }
}

public class Session
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public string? RefreshTokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public User? User { get; set; }
}

public class Character
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Class { get; set; }
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public User? User { get; set; }
}

public class PlayerStats
{
    public Guid UserId { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int EloRating { get; set; } = 1000;
    public int HighestWaveSolo { get; set; }
    public long TotalUnitsKilled { get; set; }
    public long TotalTowersBuilt { get; set; }
    public long TotalGoldEarned { get; set; }
    public long TotalPlayTimeSeconds { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}

public class PlayerTower
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long Experience { get; set; }
    public int Level { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<PlayerTowerSkill> AllocatedSkills { get; set; } = [];
    public ICollection<PlayerTowerEquipment> Equipment { get; set; } = [];
}

public class TowerSkillNode
{
    public Guid Id { get; set; }
    public required string NodeId { get; set; }
    public short Tier { get; set; }
    public short PositionX { get; set; }
    public short PositionY { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public short SkillPointsCost { get; set; } = 1;
    public int RequiredTowerLevel { get; set; } = 1;
    public TowerBonusType BonusType { get; set; }
    public decimal BonusValue { get; set; }
    public decimal BonusValuePerRank { get; set; }
    public short MaxRanks { get; set; } = 1;
    public string[] PrerequisiteNodeIds { get; set; } = [];
}

public class PlayerTowerSkill
{
    public Guid Id { get; set; }
    public Guid PlayerTowerId { get; set; }
    public Guid SkillNodeId { get; set; }
    public short RanksAllocated { get; set; } = 1;
    public DateTime AllocatedAt { get; set; }

    public PlayerTower? PlayerTower { get; set; }
    public TowerSkillNode? SkillNode { get; set; }
}

public class ItemBase
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ItemType ItemType { get; set; }
    public WeaponSubtype? WeaponSubtype { get; set; }
    public AccessorySubtype? AccessorySubtype { get; set; }
    public int? BaseDamage { get; set; }
    public decimal? BaseRange { get; set; }
    public decimal? BaseAttackSpeed { get; set; }
    public bool HitsMultiple { get; set; }
    public int MaxTargets { get; set; } = 1;
    public int BaseHpBonus { get; set; }
    public decimal BaseBlockChance { get; set; }
    public int RequiredTowerLevel { get; set; } = 1;
    public string? Icon { get; set; }
}

public class ItemAffix
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string DisplayTemplate { get; set; }
    public AffixType AffixType { get; set; }
    public TowerBonusType BonusType { get; set; }
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public int Weight { get; set; } = 100;
    public ItemType[] AllowedItemTypes { get; set; } = [];
}

public class PlayerItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ItemBaseId { get; set; }
    public ItemRarity Rarity { get; set; }
    public string AffixesJson { get; set; } = "[]";
    public DateTime ObtainedAt { get; set; }
    public string? ObtainedFrom { get; set; }
    public Guid? MatchId { get; set; }

    public User? User { get; set; }
    public ItemBase? ItemBase { get; set; }
    public PlayerTowerEquipment? EquippedOn { get; set; }
}

public class PlayerTowerEquipment
{
    public Guid Id { get; set; }
    public Guid PlayerTowerId { get; set; }
    public EquipmentSlot Slot { get; set; }
    public Guid? ItemId { get; set; }

    public PlayerTower? PlayerTower { get; set; }
    public PlayerItem? Item { get; set; }
}

/// <summary>
/// Represents a rolled affix on an item, stored as JSON in PlayerItem.AffixesJson
/// </summary>
public sealed record RolledAffix(
    Guid AffixId,
    string Name,
    string DisplayText,
    AffixType AffixType,
    TowerBonusType BonusType,
    decimal RolledValue
);
