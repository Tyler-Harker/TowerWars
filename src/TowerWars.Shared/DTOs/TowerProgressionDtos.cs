namespace TowerWars.Shared.DTOs;

public enum TowerBonusType
{
    // Damage bonuses
    DamagePercent = 0,
    DamageFlat = 1,
    PhysicalDamagePercent = 2,
    MagicDamagePercent = 3,
    FireDamagePercent = 4,
    IceDamagePercent = 5,
    LightningDamagePercent = 6,
    PoisonDamagePercent = 7,
    FireDamageFlat = 8,
    IceDamageFlat = 9,
    LightningDamageFlat = 10,
    PoisonDamageFlat = 11,

    // Attack modifiers
    AttackSpeedPercent = 20,
    RangePercent = 21,
    CritChance = 22,
    CritMultiplier = 23,

    // Defensive bonuses
    TowerHpFlat = 40,
    TowerHpPercent = 41,
    DamageReductionPercent = 42,
    BlockChance = 43,

    // Utility bonuses
    GoldFindPercent = 60,
    XpGainPercent = 61,
    LifeLeechPercent = 62,

    // Status effect bonuses
    SlowAmountPercent = 80,
    SlowDurationPercent = 81,
    SplashRadiusPercent = 82,
}

public sealed record PlayerTowerDto(
    Guid Id,
    long Experience,
    int Level,
    int AvailableSkillPoints
);

public sealed record PlayerTowerDetailDto(
    Guid Id,
    long Experience,
    long ExperienceToNextLevel,
    int Level,
    int MaxLevel,
    int AvailableSkillPoints,
    int TotalSkillPoints,
    List<SkillNodeDto> SkillNodes,
    List<AllocatedSkillDto> AllocatedSkills,
    TowerBonusSummaryDto BonusSummary
);

public sealed record SkillNodeDto(
    Guid Id,
    string NodeId,
    int Tier,
    int PositionX,
    int PositionY,
    string Name,
    string Description,
    int SkillPointsCost,
    int RequiredTowerLevel,
    TowerBonusType BonusType,
    decimal BonusValue,
    decimal BonusValuePerRank,
    int MaxRanks,
    string[] PrerequisiteNodeIds
);

public sealed record AllocatedSkillDto(
    Guid SkillNodeId,
    string NodeId,
    int RanksAllocated,
    DateTime AllocatedAt
);

public sealed record TowerBonusSummaryDto(
    decimal DamagePercent,
    decimal DamageFlat,
    decimal AttackSpeedPercent,
    decimal RangePercent,
    decimal CritChance,
    decimal CritMultiplier,
    decimal TowerHpFlat,
    decimal TowerHpPercent,
    decimal DamageReductionPercent,
    decimal GoldFindPercent,
    decimal XpGainPercent,
    Dictionary<TowerBonusType, decimal> AllBonuses
);

public sealed record AllocateSkillRequest(
    Guid PlayerTowerId,
    Guid SkillNodeId,
    int RanksToAllocate = 1
);

public sealed record AllocateSkillResponse(
    bool Success,
    string? Error,
    int RemainingSkillPoints,
    AllocatedSkillDto? AllocatedSkill
);

public sealed record ResetSkillsRequest(
    Guid PlayerTowerId
);

public sealed record ResetSkillsResponse(
    bool Success,
    string? Error,
    int RefundedSkillPoints
);
