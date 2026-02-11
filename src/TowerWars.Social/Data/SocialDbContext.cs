using Microsoft.EntityFrameworkCore;

namespace TowerWars.Social.Data;

public class SocialDbContext : DbContext
{
    public SocialDbContext(DbContextOptions<SocialDbContext> options) : base(options) { }

    public DbSet<Friend> Friends => Set<Friend>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyMember> PartyMembers => Set<PartyMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Friend>(entity =>
        {
            entity.ToTable("friends");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.FriendId).HasColumnName("friend_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => new { e.UserId, e.FriendId }).IsUnique();
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Channel).HasColumnName("channel");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.RecipientId).HasColumnName("recipient_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.SentAt).HasColumnName("sent_at");
        });

        modelBuilder.Entity<Party>(entity =>
        {
            entity.ToTable("parties");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<PartyMember>(entity =>
        {
            entity.ToTable("party_members");
            entity.HasKey(e => new { e.PartyId, e.UserId });
        });
    }
}

public class Friend
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FriendId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ChatMessage
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = "global";
    public Guid SenderId { get; set; }
    public Guid? RecipientId { get; set; }
    public string Content { get; set; } = "";
    public DateTime SentAt { get; set; }
}

public class Party
{
    public Guid Id { get; set; }
    public Guid LeaderId { get; set; }
    public int MaxSize { get; set; } = 6;
    public DateTime CreatedAt { get; set; }
    public ICollection<PartyMember> Members { get; set; } = [];
}

public class PartyMember
{
    public Guid PartyId { get; set; }
    public Guid UserId { get; set; }
    public bool IsReady { get; set; }
    public DateTime JoinedAt { get; set; }
    public Party? Party { get; set; }
}
