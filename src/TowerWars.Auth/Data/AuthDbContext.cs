using Microsoft.EntityFrameworkCore;
using TowerWars.Auth.Models;
using TowerWars.Shared.DTOs;

namespace TowerWars.Auth.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();

    // Tower progression
    public DbSet<PlayerTower> PlayerTowers => Set<PlayerTower>();
    public DbSet<TowerSkillNode> TowerSkillNodes => Set<TowerSkillNode>();
    public DbSet<PlayerTowerSkill> PlayerTowerSkills => Set<PlayerTowerSkill>();

    // Equipment
    public DbSet<ItemBase> ItemBases => Set<ItemBase>();
    public DbSet<ItemAffix> ItemAffixes => Set<ItemAffix>();
    public DbSet<PlayerItem> PlayerItems => Set<PlayerItem>();
    public DbSet<PlayerTowerEquipment> PlayerTowerEquipment => Set<PlayerTowerEquipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(32);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.BannedUntil).HasColumnName("banned_until");
            entity.Property(e => e.BanReason).HasColumnName("ban_reason");
            entity.Property(e => e.InventorySlots).HasColumnName("inventory_slots");

            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(255);
            entity.Property(e => e.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(255);
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<Character>(entity =>
        {
            entity.ToTable("characters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(32);
            entity.Property(e => e.Class).HasColumnName("class").HasMaxLength(32);
            entity.Property(e => e.Level).HasColumnName("level");
            entity.Property(e => e.Experience).HasColumnName("experience");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Characters)
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<PlayerStats>(entity =>
        {
            entity.ToTable("player_stats");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Wins).HasColumnName("wins");
            entity.Property(e => e.Losses).HasColumnName("losses");
            entity.Property(e => e.EloRating).HasColumnName("elo_rating");
            entity.Property(e => e.HighestWaveSolo).HasColumnName("highest_wave_solo");
            entity.Property(e => e.TotalUnitsKilled).HasColumnName("total_units_killed");
            entity.Property(e => e.TotalTowersBuilt).HasColumnName("total_towers_built");
            entity.Property(e => e.TotalGoldEarned).HasColumnName("total_gold_earned");
            entity.Property(e => e.TotalPlayTimeSeconds).HasColumnName("total_play_time_seconds");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.User)
                .WithOne(u => u.Stats)
                .HasForeignKey<PlayerStats>(e => e.UserId);
        });

        // Tower Progression
        modelBuilder.Entity<PlayerTower>(entity =>
        {
            entity.ToTable("player_towers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TowerType).HasColumnName("tower_type").HasConversion<short>();
            entity.Property(e => e.Experience).HasColumnName("experience");
            entity.Property(e => e.Level).HasColumnName("level");
            entity.Property(e => e.Unlocked).HasColumnName("unlocked");
            entity.Property(e => e.UnlockedAt).HasColumnName("unlocked_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => new { e.UserId, e.TowerType }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany(u => u.PlayerTowers)
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<TowerSkillNode>(entity =>
        {
            entity.ToTable("tower_skill_nodes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TowerType).HasColumnName("tower_type").HasConversion<short>();
            entity.Property(e => e.NodeId).HasColumnName("node_id").HasMaxLength(32);
            entity.Property(e => e.Tier).HasColumnName("tier");
            entity.Property(e => e.PositionX).HasColumnName("position_x");
            entity.Property(e => e.PositionY).HasColumnName("position_y");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(64);
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.SkillPointsCost).HasColumnName("skill_points_cost");
            entity.Property(e => e.RequiredTowerLevel).HasColumnName("required_tower_level");
            entity.Property(e => e.BonusType).HasColumnName("bonus_type").HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.BonusValue).HasColumnName("bonus_value");
            entity.Property(e => e.BonusValuePerRank).HasColumnName("bonus_value_per_rank");
            entity.Property(e => e.MaxRanks).HasColumnName("max_ranks");
            entity.Property(e => e.PrerequisiteNodeIds).HasColumnName("prerequisite_node_ids");

            entity.HasIndex(e => new { e.TowerType, e.NodeId }).IsUnique();
        });

        modelBuilder.Entity<PlayerTowerSkill>(entity =>
        {
            entity.ToTable("player_tower_skills");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PlayerTowerId).HasColumnName("player_tower_id");
            entity.Property(e => e.SkillNodeId).HasColumnName("skill_node_id");
            entity.Property(e => e.RanksAllocated).HasColumnName("ranks_allocated");
            entity.Property(e => e.AllocatedAt).HasColumnName("allocated_at");

            entity.HasIndex(e => new { e.PlayerTowerId, e.SkillNodeId }).IsUnique();

            entity.HasOne(e => e.PlayerTower)
                .WithMany(pt => pt.AllocatedSkills)
                .HasForeignKey(e => e.PlayerTowerId);

            entity.HasOne(e => e.SkillNode)
                .WithMany()
                .HasForeignKey(e => e.SkillNodeId);
        });

        // Equipment System
        modelBuilder.Entity<ItemBase>(entity =>
        {
            entity.ToTable("item_bases");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(64);
            entity.Property(e => e.ItemType).HasColumnName("item_type").HasConversion<short>();
            entity.Property(e => e.WeaponSubtype).HasColumnName("weapon_subtype").HasConversion<short?>();
            entity.Property(e => e.AccessorySubtype).HasColumnName("accessory_subtype").HasConversion<short?>();
            entity.Property(e => e.BaseDamage).HasColumnName("base_damage");
            entity.Property(e => e.BaseRange).HasColumnName("base_range");
            entity.Property(e => e.BaseAttackSpeed).HasColumnName("base_attack_speed");
            entity.Property(e => e.HitsMultiple).HasColumnName("hits_multiple");
            entity.Property(e => e.MaxTargets).HasColumnName("max_targets");
            entity.Property(e => e.BaseHpBonus).HasColumnName("base_hp_bonus");
            entity.Property(e => e.BaseBlockChance).HasColumnName("base_block_chance");
            entity.Property(e => e.RequiredTowerLevel).HasColumnName("required_tower_level");
            entity.Property(e => e.Icon).HasColumnName("icon").HasMaxLength(64);

            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<ItemAffix>(entity =>
        {
            entity.ToTable("item_affixes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(64);
            entity.Property(e => e.DisplayTemplate).HasColumnName("display_template").HasMaxLength(128);
            entity.Property(e => e.AffixType).HasColumnName("affix_type").HasConversion<short>();
            entity.Property(e => e.BonusType).HasColumnName("bonus_type").HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.MinValue).HasColumnName("min_value");
            entity.Property(e => e.MaxValue).HasColumnName("max_value");
            entity.Property(e => e.Weight).HasColumnName("weight");
            entity.Property(e => e.AllowedItemTypes).HasColumnName("allowed_item_types")
                .HasConversion(
                    v => v.Select(x => (short)x).ToArray(),
                    v => v.Select(x => (ItemType)x).ToArray()
                );

            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<PlayerItem>(entity =>
        {
            entity.ToTable("player_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ItemBaseId).HasColumnName("item_base_id");
            entity.Property(e => e.Rarity).HasColumnName("rarity").HasConversion<short>();
            entity.Property(e => e.AffixesJson).HasColumnName("affixes").HasColumnType("jsonb");
            entity.Property(e => e.ObtainedAt).HasColumnName("obtained_at");
            entity.Property(e => e.ObtainedFrom).HasColumnName("obtained_from").HasMaxLength(32);
            entity.Property(e => e.MatchId).HasColumnName("match_id");

            entity.HasOne(e => e.User)
                .WithMany(u => u.PlayerItems)
                .HasForeignKey(e => e.UserId);

            entity.HasOne(e => e.ItemBase)
                .WithMany()
                .HasForeignKey(e => e.ItemBaseId);
        });

        modelBuilder.Entity<PlayerTowerEquipment>(entity =>
        {
            entity.ToTable("player_tower_equipment");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PlayerTowerId).HasColumnName("player_tower_id");
            entity.Property(e => e.Slot).HasColumnName("slot").HasConversion<short>();
            entity.Property(e => e.ItemId).HasColumnName("item_id");

            entity.HasIndex(e => new { e.PlayerTowerId, e.Slot }).IsUnique();
            entity.HasIndex(e => e.ItemId).IsUnique();

            entity.HasOne(e => e.PlayerTower)
                .WithMany(pt => pt.Equipment)
                .HasForeignKey(e => e.PlayerTowerId);

            entity.HasOne(e => e.Item)
                .WithOne(i => i.EquippedOn)
                .HasForeignKey<PlayerTowerEquipment>(e => e.ItemId);
        });
    }
}
