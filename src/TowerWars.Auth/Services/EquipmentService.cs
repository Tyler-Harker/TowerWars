using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TowerWars.Auth.Data;
using TowerWars.Auth.Models;
using TowerWars.Shared.Constants;
using TowerWars.Shared.DTOs;

namespace TowerWars.Auth.Services;

public interface IEquipmentService
{
    Task<PlayerInventoryResponse> GetInventoryAsync(Guid userId, int page = 1, int pageSize = 50);
    Task<TowerEquipmentDto?> GetTowerEquipmentAsync(Guid userId, byte towerType);
    Task<EquipItemResponse> EquipItemAsync(Guid userId, EquipItemRequest request);
    Task<UnequipItemResponse> UnequipItemAsync(Guid userId, UnequipItemRequest request);
    Task<DeleteItemResponse> DeleteItemAsync(Guid userId, DeleteItemRequest request);
    Task<ItemDto?> GenerateItemAsync(GenerateItemRequest request);
    Task<TowerBonusSummaryDto> GetEquipmentBonusesAsync(Guid userId, byte towerType);
}

public class EquipmentService : IEquipmentService
{
    private readonly AuthDbContext _db;
    private readonly Random _random = new();

    public EquipmentService(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<PlayerInventoryResponse> GetInventoryAsync(Guid userId, int page = 1, int pageSize = 50)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return new PlayerInventoryResponse([], 0, 50, 0, page, pageSize);

        var query = _db.PlayerItems
            .Where(i => i.UserId == userId)
            .Include(i => i.ItemBase)
            .Include(i => i.EquippedOn)
            .OrderByDescending(i => i.ObtainedAt);

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PlayerInventoryResponse(
            items.Select(i => ToItemDto(i)).ToList(),
            totalItems,
            user.InventorySlots,
            totalItems,
            page,
            pageSize
        );
    }

    public async Task<TowerEquipmentDto?> GetTowerEquipmentAsync(Guid userId, byte towerType)
    {
        var tower = await _db.PlayerTowers
            .Where(pt => pt.UserId == userId && pt.TowerType == (TowerType)towerType)
            .Include(pt => pt.Equipment)
                .ThenInclude(e => e.Item)
                    .ThenInclude(i => i!.ItemBase)
            .FirstOrDefaultAsync();

        if (tower == null) return null;

        var equipment = new List<EquippedItemDto>();
        foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
        {
            var equipped = tower.Equipment.FirstOrDefault(e => e.Slot == slot);
            equipment.Add(new EquippedItemDto(
                slot,
                equipped?.Item != null ? ToItemDto(equipped.Item) : null
            ));
        }

        return new TowerEquipmentDto(
            tower.Id,
            towerType,
            equipment,
            CalculateEquipmentBonuses(tower.Equipment)
        );
    }

    public async Task<EquipItemResponse> EquipItemAsync(Guid userId, EquipItemRequest request)
    {
        var item = await _db.PlayerItems
            .Include(i => i.ItemBase)
            .Include(i => i.EquippedOn)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.UserId == userId);

        if (item == null)
            return new EquipItemResponse(false, "Item not found", null, null);

        var tower = await _db.PlayerTowers
            .Where(pt => pt.Id == request.PlayerTowerId && pt.UserId == userId)
            .Include(pt => pt.Equipment)
                .ThenInclude(e => e.Item)
                    .ThenInclude(i => i!.ItemBase)
            .FirstOrDefaultAsync();

        if (tower == null)
            return new EquipItemResponse(false, "Tower not found", null, null);

        if (!tower.Unlocked)
            return new EquipItemResponse(false, "Tower is not unlocked", null, null);

        if (item.ItemBase!.RequiredTowerLevel > tower.Level)
            return new EquipItemResponse(false, $"Tower level {item.ItemBase.RequiredTowerLevel} required", null, null);

        // Validate slot matches item type
        var validSlot = (request.Slot, item.ItemBase.ItemType) switch
        {
            (EquipmentSlot.Weapon, ItemType.Weapon) => true,
            (EquipmentSlot.Shield, ItemType.Shield) => true,
            (EquipmentSlot.Accessory1 or EquipmentSlot.Accessory2 or EquipmentSlot.Accessory3, ItemType.Accessory) => true,
            _ => false
        };

        if (!validSlot)
            return new EquipItemResponse(false, "Item type does not match slot", null, null);

        // If item is already equipped elsewhere, unequip it first
        if (item.EquippedOn != null)
        {
            _db.PlayerTowerEquipment.Remove(item.EquippedOn);
        }

        // Check if slot is occupied
        var existingEquipment = tower.Equipment.FirstOrDefault(e => e.Slot == request.Slot);
        ItemDto? unequippedItem = null;

        if (existingEquipment != null)
        {
            if (existingEquipment.Item != null)
            {
                unequippedItem = ToItemDto(existingEquipment.Item);
            }
            existingEquipment.ItemId = item.Id;
            existingEquipment.Item = item;
        }
        else
        {
            var newEquipment = new PlayerTowerEquipment
            {
                Id = Guid.NewGuid(),
                PlayerTowerId = tower.Id,
                Slot = request.Slot,
                ItemId = item.Id
            };
            _db.PlayerTowerEquipment.Add(newEquipment);
        }

        await _db.SaveChangesAsync();

        return new EquipItemResponse(true, null, ToItemDto(item), unequippedItem);
    }

    public async Task<UnequipItemResponse> UnequipItemAsync(Guid userId, UnequipItemRequest request)
    {
        var tower = await _db.PlayerTowers
            .Where(pt => pt.Id == request.PlayerTowerId && pt.UserId == userId)
            .Include(pt => pt.Equipment)
                .ThenInclude(e => e.Item)
                    .ThenInclude(i => i!.ItemBase)
            .FirstOrDefaultAsync();

        if (tower == null)
            return new UnequipItemResponse(false, "Tower not found", null);

        var equipment = tower.Equipment.FirstOrDefault(e => e.Slot == request.Slot);
        if (equipment == null || equipment.Item == null)
            return new UnequipItemResponse(false, "Slot is empty", null);

        var unequippedItem = ToItemDto(equipment.Item);
        equipment.ItemId = null;
        equipment.Item = null;

        await _db.SaveChangesAsync();

        return new UnequipItemResponse(true, null, unequippedItem);
    }

    public async Task<DeleteItemResponse> DeleteItemAsync(Guid userId, DeleteItemRequest request)
    {
        var item = await _db.PlayerItems
            .Include(i => i.EquippedOn)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.UserId == userId);

        if (item == null)
            return new DeleteItemResponse(false, "Item not found");

        if (item.EquippedOn != null)
            return new DeleteItemResponse(false, "Cannot delete equipped item");

        _db.PlayerItems.Remove(item);
        await _db.SaveChangesAsync();

        return new DeleteItemResponse(true, null);
    }

    public async Task<ItemDto?> GenerateItemAsync(GenerateItemRequest request)
    {
        var user = await _db.Users.FindAsync(request.UserId);
        if (user == null) return null;

        // Check inventory space
        var currentItemCount = await _db.PlayerItems.CountAsync(i => i.UserId == request.UserId);
        if (currentItemCount >= user.InventorySlots) return null;

        // Roll rarity
        var rarity = request.ForcedRarity ?? RollRarity();

        // Get available item bases
        var itemBases = await _db.ItemBases.ToListAsync();
        if (request.ForcedItemType.HasValue)
            itemBases = itemBases.Where(ib => ib.ItemType == request.ForcedItemType.Value).ToList();

        if (itemBases.Count == 0) return null;

        var selectedBase = itemBases[_random.Next(itemBases.Count)];

        // Roll affixes based on rarity
        var affixes = await RollAffixesAsync(selectedBase.ItemType, rarity);

        var item = new PlayerItem
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            ItemBaseId = selectedBase.Id,
            Rarity = rarity,
            AffixesJson = JsonSerializer.Serialize(affixes),
            ObtainedAt = DateTime.UtcNow,
            ObtainedFrom = request.Source,
            MatchId = request.MatchId,
            ItemBase = selectedBase
        };

        _db.PlayerItems.Add(item);
        await _db.SaveChangesAsync();

        return ToItemDto(item, affixes);
    }

    public async Task<TowerBonusSummaryDto> GetEquipmentBonusesAsync(Guid userId, byte towerType)
    {
        var tower = await _db.PlayerTowers
            .Where(pt => pt.UserId == userId && pt.TowerType == (TowerType)towerType)
            .Include(pt => pt.Equipment)
                .ThenInclude(e => e.Item)
                    .ThenInclude(i => i!.ItemBase)
            .FirstOrDefaultAsync();

        if (tower == null)
            return CreateEmptyBonusSummary();

        return CalculateEquipmentBonuses(tower.Equipment);
    }

    private ItemRarity RollRarity()
    {
        var roll = _random.Next(100);
        if (roll < ItemDropConstants.RareWeight)
            return ItemRarity.Rare;
        if (roll < ItemDropConstants.RareWeight + ItemDropConstants.MagicWeight)
            return ItemRarity.Magic;
        return ItemRarity.Normal;
    }

    private async Task<List<RolledAffix>> RollAffixesAsync(ItemType itemType, ItemRarity rarity)
    {
        var (minAffixes, maxAffixes) = rarity switch
        {
            ItemRarity.Rare => ItemDropConstants.RareAffixCount,
            ItemRarity.Magic => ItemDropConstants.MagicAffixCount,
            _ => ItemDropConstants.NormalAffixCount
        };

        var affixCount = _random.Next(minAffixes, maxAffixes + 1);
        if (affixCount == 0) return [];

        var availableAffixes = await _db.ItemAffixes
            .Where(a => a.AllowedItemTypes.Contains(itemType) || a.AllowedItemTypes.Length == 0)
            .ToListAsync();

        var rolledAffixes = new List<RolledAffix>();
        var usedAffixIds = new HashSet<Guid>();
        var hasPrefixes = 0;
        var hasSuffixes = 0;

        for (int i = 0; i < affixCount && availableAffixes.Count > 0; i++)
        {
            // Weight-based selection
            var totalWeight = availableAffixes.Sum(a => a.Weight);
            var roll = _random.Next(totalWeight);
            var cumulative = 0;
            ItemAffix? selectedAffix = null;

            foreach (var affix in availableAffixes)
            {
                cumulative += affix.Weight;
                if (roll < cumulative)
                {
                    selectedAffix = affix;
                    break;
                }
            }

            if (selectedAffix == null) continue;
            if (usedAffixIds.Contains(selectedAffix.Id)) continue;

            // Limit prefixes and suffixes
            if (selectedAffix.AffixType == AffixType.Prefix && hasPrefixes >= 2) continue;
            if (selectedAffix.AffixType == AffixType.Suffix && hasSuffixes >= 2) continue;

            // Roll value
            var range = selectedAffix.MaxValue - selectedAffix.MinValue;
            var rolledValue = selectedAffix.MinValue + (decimal)_random.NextDouble() * range;
            rolledValue = Math.Round(rolledValue, 2);

            var displayText = selectedAffix.DisplayTemplate
                .Replace("{value}", rolledValue.ToString("0.##"));

            rolledAffixes.Add(new RolledAffix(
                selectedAffix.Id,
                selectedAffix.Name,
                displayText,
                selectedAffix.AffixType,
                selectedAffix.BonusType,
                rolledValue
            ));

            usedAffixIds.Add(selectedAffix.Id);
            if (selectedAffix.AffixType == AffixType.Prefix) hasPrefixes++;
            else hasSuffixes++;
        }

        return rolledAffixes;
    }

    private ItemDto ToItemDto(PlayerItem item, List<RolledAffix>? affixes = null)
    {
        affixes ??= JsonSerializer.Deserialize<List<RolledAffix>>(item.AffixesJson) ?? [];

        var displayName = GenerateDisplayName(item.ItemBase!.Name, affixes, item.Rarity);

        return new ItemDto(
            item.Id,
            ToItemBaseDto(item.ItemBase!),
            item.Rarity,
            affixes.Select(a => new ItemAffixDto(
                a.AffixId,
                a.Name,
                a.DisplayText,
                a.AffixType,
                a.BonusType,
                a.RolledValue
            )).ToList(),
            displayName,
            item.ObtainedAt,
            item.ObtainedFrom,
            item.MatchId,
            item.EquippedOn != null,
            item.EquippedOn?.PlayerTowerId
        );
    }

    private static ItemBaseDto ToItemBaseDto(ItemBase itemBase) => new(
        itemBase.Id,
        itemBase.Name,
        itemBase.ItemType,
        itemBase.WeaponSubtype,
        itemBase.AccessorySubtype,
        itemBase.BaseDamage,
        itemBase.BaseRange,
        itemBase.BaseAttackSpeed,
        itemBase.HitsMultiple,
        itemBase.MaxTargets,
        itemBase.BaseHpBonus,
        itemBase.BaseBlockChance,
        itemBase.RequiredTowerLevel,
        itemBase.Icon
    );

    private static string GenerateDisplayName(string baseName, List<RolledAffix> affixes, ItemRarity rarity)
    {
        var prefix = affixes.FirstOrDefault(a => a.AffixType == AffixType.Prefix);
        var suffix = affixes.FirstOrDefault(a => a.AffixType == AffixType.Suffix);

        var name = baseName;
        if (prefix != null)
            name = $"{prefix.Name} {name}";
        if (suffix != null)
            name = $"{name} {suffix.Name}";

        return name;
    }

    private TowerBonusSummaryDto CalculateEquipmentBonuses(IEnumerable<PlayerTowerEquipment> equipment)
    {
        var bonuses = new Dictionary<TowerBonusType, decimal>();

        foreach (var eq in equipment)
        {
            if (eq.Item == null) continue;

            var affixes = JsonSerializer.Deserialize<List<RolledAffix>>(eq.Item.AffixesJson) ?? [];

            foreach (var affix in affixes)
            {
                if (bonuses.ContainsKey(affix.BonusType))
                    bonuses[affix.BonusType] += affix.RolledValue;
                else
                    bonuses[affix.BonusType] = affix.RolledValue;
            }
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
