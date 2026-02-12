using MessagePack;

namespace TowerWars.Shared.Protocol;

[MessagePackObject]
public sealed class PlayerDataRequestPacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.PlayerDataRequest;
}

[MessagePackObject]
public sealed class PlayerTowersResponsePacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.PlayerTowersResponse;

    [Key(0)]
    public required bool Success { get; init; }

    [Key(1)]
    public string? ErrorMessage { get; init; }

    [Key(2)]
    public required WirePlayerTower[] Towers { get; init; }
}

[MessagePackObject]
public sealed class PlayerItemsResponsePacket : IPacket
{
    [IgnoreMember]
    public PacketType Type => PacketType.PlayerItemsResponse;

    [Key(0)]
    public required bool Success { get; init; }

    [Key(1)]
    public string? ErrorMessage { get; init; }

    [Key(2)]
    public required WirePlayerItem[] Items { get; init; }
}

[MessagePackObject]
public sealed class WirePlayerTower
{
    [Key(0)]
    public required Guid Id { get; init; }

    [Key(1)]
    public required Guid UserId { get; init; }

    [Key(2)]
    public required string Name { get; init; }

    [Key(3)]
    public required byte WeaponType { get; init; }

    [Key(4)]
    public required byte DamageType { get; init; }

    [Key(5)]
    public required int Level { get; init; }

    [Key(6)]
    public required long Experience { get; init; }

    [Key(7)]
    public required int SkillPoints { get; init; }

    [Key(8)]
    public required float BaseDamage { get; init; }

    [Key(9)]
    public required float BaseAttackSpeed { get; init; }

    [Key(10)]
    public required float BaseRange { get; init; }

    [Key(11)]
    public required float BaseCritChance { get; init; }

    [Key(12)]
    public required float BaseCritDamage { get; init; }

    [Key(13)]
    public required WireTowerSkillAllocation[] SkillAllocations { get; init; }

    [Key(14)]
    public required WireTowerEquippedItem[] EquippedItems { get; init; }
}

[MessagePackObject]
public sealed class WireTowerSkillAllocation
{
    [Key(0)]
    public required Guid Id { get; init; }

    [Key(1)]
    public required Guid TowerId { get; init; }

    [Key(2)]
    public required string SkillId { get; init; }

    [Key(3)]
    public required int Points { get; init; }
}

[MessagePackObject]
public sealed class WireTowerEquippedItem
{
    [Key(0)]
    public required Guid Id { get; init; }

    [Key(1)]
    public required Guid TowerId { get; init; }

    [Key(2)]
    public required Guid ItemId { get; init; }

    [Key(3)]
    public required string Slot { get; init; }

    [Key(4)]
    public WirePlayerItem? Item { get; init; }
}

[MessagePackObject]
public sealed class WirePlayerItem
{
    [Key(0)]
    public required Guid Id { get; init; }

    [Key(1)]
    public required Guid UserId { get; init; }

    [Key(2)]
    public required string Name { get; init; }

    [Key(3)]
    public required byte ItemType { get; init; }

    [Key(4)]
    public required byte Rarity { get; init; }

    [Key(5)]
    public required int ItemLevel { get; init; }

    [Key(6)]
    public required string BaseStatsJson { get; init; }

    [Key(7)]
    public required string AffixesJson { get; init; }

    [Key(8)]
    public required bool IsEquipped { get; init; }

    [Key(9)]
    public required long DroppedAtUnixMs { get; init; }

    [Key(10)]
    public long? CollectedAtUnixMs { get; init; }
}
