using Microsoft.EntityFrameworkCore;
using TarteelClone.UserService.Models;

namespace TarteelClone.UserService.Data;

public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

    public DbSet<User>                  Users                { get; set; }
    public DbSet<RecitationSession>     RecitationSessions   { get; set; }
    public DbSet<RecitationError>       RecitationErrors     { get; set; }
    public DbSet<MemorizationProgress>  MemorizationProgress { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<RecitationSession>(e =>
        {
            e.ToTable("recitation_sessions");
            e.HasKey(s => s.Id);
            e.Property(s => s.UserId).HasColumnName("user_id");
            e.Property(s => s.StartedAt).HasColumnName("started_at");
            e.Property(s => s.EndedAt).HasColumnName("ended_at");
            e.HasOne(s => s.User).WithMany(u => u.Sessions).HasForeignKey(s => s.UserId);
        });

        modelBuilder.Entity<RecitationError>(e =>
        {
            e.ToTable("recitation_errors");
            e.HasKey(re => re.Id);
            e.Property(re => re.SessionId).HasColumnName("session_id");
            e.Property(re => re.VerseId).HasColumnName("verse_id");
            e.Property(re => re.ErrorType).HasColumnName("error_type").HasMaxLength(100);
            e.Property(re => re.Timestamp).HasColumnName("timestamp");
            e.HasOne(re => re.Session).WithMany(s => s.Errors).HasForeignKey(re => re.SessionId);
        });

        modelBuilder.Entity<MemorizationProgress>(e =>
        {
            e.ToTable("memorization_progress");
            e.HasKey(mp => mp.Id);
            e.Property(mp => mp.UserId).HasColumnName("user_id");
            e.Property(mp => mp.SurahNum).HasColumnName("surah_num");
            e.Property(mp => mp.AyahNum).HasColumnName("ayah_num");
            e.Property(mp => mp.MasteryScore).HasColumnName("mastery_score");
            e.HasIndex(mp => new { mp.UserId, mp.SurahNum, mp.AyahNum }).IsUnique();
            e.HasOne(mp => mp.User).WithMany(u => u.Progress).HasForeignKey(mp => mp.UserId);
        });
    }
}
