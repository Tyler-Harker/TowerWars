using Microsoft.EntityFrameworkCore;

namespace TowerWars.Persistence.Data;

public class PersistenceDbContext : DbContext
{
    public PersistenceDbContext(DbContextOptions<PersistenceDbContext> options) : base(options) { }

    public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();

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
