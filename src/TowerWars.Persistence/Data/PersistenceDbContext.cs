using Microsoft.EntityFrameworkCore;

namespace TowerWars.Persistence.Data;

public class PersistenceDbContext : DbContext
{
    public PersistenceDbContext(DbContextOptions<PersistenceDbContext> options) : base(options) { }

    public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();

    // Tower and item persistence
    public DbSet<PlayerTower> PlayerTowers => Set<PlayerTower>();
    public DbSet<TowerSkillAllocation> TowerSkillAllocations => Set<TowerSkillAllocation>();
    public DbSet<TowerEquippedItem> TowerEquippedItems => Set<TowerEquippedItem>();
    public DbSet<PlayerItem> PlayerItems => Set<PlayerItem>();
    public DbSet<ItemDrop> ItemDrops => Set<ItemDrop>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.ToTable("matches");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Mode).HasColumnName("mode");
            entity.Property(e => e.MapId).HasColumnName("map_id");
            entity.Property(e => e.Result).HasColumnName("result");
            entity.Property(e => e.WavesCompleted).HasColumnName("waves_completed");
            entity.Property(e => e.DurationSeconds).HasColumnName("duration_seconds");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
        });

        modelBuilder.Entity<MatchParticipant>(entity =>
        {
            entity.ToTable("match_participants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CharacterId).HasColumnName("character_id");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.Score).HasColumnName("score");
            entity.Property(e => e.UnitsKilled).HasColumnName("units_killed");
            entity.Property(e => e.TowersBuilt).HasColumnName("towers_built");
            entity.Property(e => e.GoldEarned).HasColumnName("gold_earned");
            entity.Property(e => e.DamageDealt).HasColumnName("damage_dealt");
            entity.Property(e => e.LivesLost).HasColumnName("lives_lost");
            entity.Property(e => e.Result).HasColumnName("result");
            entity.Property(e => e.EloChange).HasColumnName("elo_change");
        });

        modelBuilder.Entity<MatchEvent>(entity =>
        {
            entity.ToTable("match_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.EventType).HasColumnName("event_type");
            entity.Property(e => e.EventData).HasColumnName("event_data").HasColumnType("jsonb");
            entity.Property(e => e.Tick).HasColumnName("tick");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
        });

        // Player Towers
        modelBuilder.Entity<PlayerTower>(entity =>
        {
            entity.ToTable("player_towers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100);
            entity.Property(e => e.WeaponType).HasColumnName("weapon_type").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.DamageType).HasColumnName("damage_type").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Level).HasColumnName("level");
            entity.Property(e => e.Experience).HasColumnName("experience");
            entity.Property(e => e.SkillPoints).HasColumnName("skill_points");
            entity.Property(e => e.BaseDamage).HasColumnName("base_damage");
            entity.Property(e => e.BaseAttackSpeed).HasColumnName("base_attack_speed");
            entity.Property(e => e.BaseRange).HasColumnName("base_range");
            entity.Property(e => e.BaseCritChance).HasColumnName("base_crit_chance");
            entity.Property(e => e.BaseCritDamage).HasColumnName("base_crit_damage");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.UserId).HasDatabaseName("ix_player_towers_user_id");
        });

        // Tower Skill Allocations
        modelBuilder.Entity<TowerSkillAllocation>(entity =>
        {
            entity.ToTable("tower_skill_allocations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TowerId).HasColumnName("tower_id");
            entity.Property(e => e.SkillId).HasColumnName("skill_id").HasMaxLength(100);
            entity.Property(e => e.Points).HasColumnName("points");

            entity.HasOne(e => e.Tower)
                .WithMany(t => t.SkillAllocations)
                .HasForeignKey(e => e.TowerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.TowerId, e.SkillId }).IsUnique().HasDatabaseName("ix_tower_skill_allocations_tower_skill");
        });

        // Player Items
        modelBuilder.Entity<PlayerItem>(entity =>
        {
            entity.ToTable("player_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(e => e.ItemType).HasColumnName("item_type").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Rarity).HasColumnName("rarity").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.ItemLevel).HasColumnName("item_level");
            entity.Property(e => e.BaseStatsJson).HasColumnName("base_stats").HasColumnType("jsonb");
            entity.Property(e => e.AffixesJson).HasColumnName("affixes").HasColumnType("jsonb");
            entity.Property(e => e.IsEquipped).HasColumnName("is_equipped");
            entity.Property(e => e.DroppedAt).HasColumnName("dropped_at");
            entity.Property(e => e.CollectedAt).HasColumnName("collected_at");

            entity.HasIndex(e => e.UserId).HasDatabaseName("ix_player_items_user_id");
            entity.HasIndex(e => e.IsEquipped).HasDatabaseName("ix_player_items_is_equipped");
        });

        // Tower Equipped Items
        modelBuilder.Entity<TowerEquippedItem>(entity =>
        {
            entity.ToTable("tower_equipped_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TowerId).HasColumnName("tower_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Slot).HasColumnName("slot").HasMaxLength(50);

            entity.HasOne(e => e.Tower)
                .WithMany(t => t.EquippedItems)
                .HasForeignKey(e => e.TowerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.TowerId, e.Slot }).IsUnique().HasDatabaseName("ix_tower_equipped_items_tower_slot");
        });

        // Item Drops (pending collection)
        modelBuilder.Entity<ItemDrop>(entity =>
        {
            entity.ToTable("item_drops");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id").HasMaxLength(100);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(e => e.ItemType).HasColumnName("item_type").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Rarity).HasColumnName("rarity").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.ItemLevel).HasColumnName("item_level");
            entity.Property(e => e.BaseStatsJson).HasColumnName("base_stats").HasColumnType("jsonb");
            entity.Property(e => e.AffixesJson).HasColumnName("affixes").HasColumnType("jsonb");
            entity.Property(e => e.PositionX).HasColumnName("position_x");
            entity.Property(e => e.PositionY).HasColumnName("position_y");
            entity.Property(e => e.DroppedAt).HasColumnName("dropped_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsCollected).HasColumnName("is_collected");

            entity.HasIndex(e => new { e.UserId, e.SessionId, e.IsCollected }).HasDatabaseName("ix_item_drops_user_session");
            entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("ix_item_drops_expires_at");
        });
    }
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
}

public class Match
{
    public Guid Id { get; set; }
    public string Mode { get; set; } = "";
    public string? MapId { get; set; }
    public string? Result { get; set; }
    public int WavesCompleted { get; set; }
    public float DurationSeconds { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public ICollection<MatchParticipant> Participants { get; set; } = [];
}

public class MatchParticipant
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CharacterId { get; set; }
    public short TeamId { get; set; }
    public int Score { get; set; }
    public int UnitsKilled { get; set; }
    public int TowersBuilt { get; set; }
    public int GoldEarned { get; set; }
    public int DamageDealt { get; set; }
    public int LivesLost { get; set; }
    public string? Result { get; set; }
    public int EloChange { get; set; }

    public Match? Match { get; set; }
}

public class MatchEvent
{
    public long Id { get; set; }
    public Guid MatchId { get; set; }
    public string EventType { get; set; } = "";
    public string EventData { get; set; } = "{}";
    public int? Tick { get; set; }
    public DateTime OccurredAt { get; set; }
}
