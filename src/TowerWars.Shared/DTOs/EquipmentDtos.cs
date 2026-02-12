namespace TowerWars.Shared.DTOs;

public enum ItemRarity : byte
{
    Normal = 0,
    Magic = 1,
    Rare = 2
}

public enum ItemType : byte
{
    Weapon = 0,
    Shield = 1,
    Accessory = 2
}

public enum WeaponSubtype : byte
{
    Bow = 0,      // Fast projectile, long range
    Sword = 1,    // Melee arc sweep, hits multiple
    Club = 2,     // Heavy slam, stun chance
    Wand = 3,     // Magic bolt, converts to magic damage
    Axe = 4       // Thrown + return, hits twice
}

public enum AccessorySubtype : byte
{
    Ring = 0,     // Offensive bonuses
    Amulet = 1,   // Utility bonuses
    Charm = 2     // Elemental bonuses
}

public enum EquipmentSlot : byte
{
    Weapon = 0,
    Shield = 1,
    Accessory1 = 2,
    Accessory2 = 3,
    Accessory3 = 4
}

public enum AffixType : byte
{
    Prefix = 0,
    Suffix = 1
}

public sealed record ItemBaseDto(
    Guid Id,
    string Name,
    ItemType ItemType,
    WeaponSubtype? WeaponSubtype,
    AccessorySubtype? AccessorySubtype,
    int? BaseDamage,
    decimal? BaseRange,
    decimal? BaseAttackSpeed,
    bool HitsMultiple,
    int MaxTargets,
    int BaseHpBonus,
    decimal BaseBlockChance,
    int RequiredTowerLevel,
    string? Icon
);

public sealed record ItemAffixDto(
    Guid AffixId,
    string Name,
    string DisplayText,
    AffixType AffixType,
    TowerBonusType BonusType,
    decimal RolledValue
);

public sealed record ItemDto(
    Guid Id,
    ItemBaseDto Base,
    ItemRarity Rarity,
    List<ItemAffixDto> Affixes,
    string DisplayName,
    DateTime ObtainedAt,
    string? ObtainedFrom,
    Guid? MatchId,
    bool IsEquipped,
    Guid? EquippedOnTowerId
);

public sealed record EquippedItemDto(
    EquipmentSlot Slot,
    ItemDto? Item
);

public sealed record TowerEquipmentDto(
    Guid PlayerTowerId,
    List<EquippedItemDto> Equipment,
    TowerBonusSummaryDto EquipmentBonuses
);

public sealed record PlayerInventoryResponse(
    List<ItemDto> Items,
    int TotalItems,
    int InventorySlots,
    int UsedSlots,
    int Page,
    int PageSize
);

public sealed record EquipItemRequest(
    Guid ItemId,
    Guid PlayerTowerId,
    EquipmentSlot Slot
);

public sealed record EquipItemResponse(
    bool Success,
    string? Error,
    ItemDto? EquippedItem,
    ItemDto? UnequippedItem  // If slot was already occupied
);

public sealed record UnequipItemRequest(
    Guid PlayerTowerId,
    EquipmentSlot Slot
);

public sealed record UnequipItemResponse(
    bool Success,
    string? Error,
    ItemDto? UnequippedItem
);

public sealed record DeleteItemRequest(
    Guid ItemId
);

public sealed record DeleteItemResponse(
    bool Success,
    string? Error
);

public sealed record GenerateItemRequest(
    Guid UserId,
    ItemRarity? ForcedRarity,
    ItemType? ForcedItemType,
    string Source,
    Guid? MatchId
);

public sealed record WeaponAttackStyleDto(
    WeaponSubtype Subtype,
    int Damage,
    decimal Range,
    decimal AttackSpeed,
    bool HitsMultiple,
    int MaxTargets,
    bool IsProjectile,
    bool ReturnsToSource  // For axe
);
