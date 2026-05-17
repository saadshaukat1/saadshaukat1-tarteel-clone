using Microsoft.EntityFrameworkCore;
using TarteelClone.QuranEngine.Models;

namespace TarteelClone.QuranEngine.Data;

public class QuranDbContext : DbContext
{
    public QuranDbContext(DbContextOptions<QuranDbContext> options) : base(options) { }

    public DbSet<Verse>      Verses       { get; set; }
    public DbSet<Translation> Translations { get; set; }
    public DbSet<Tafsir>      Tafsirs      { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Verse>(e =>
        {
            e.ToTable("verses");
            e.HasKey(v => v.Id);
            e.Property(v => v.SurahNum).HasColumnName("surah_num").IsRequired();
            e.Property(v => v.AyahNum).HasColumnName("ayah_num").IsRequired();
            e.Property(v => v.ArabicText).HasColumnName("arabic_text").IsRequired();
            e.Property(v => v.UthmaniText).HasColumnName("uthmani_text");
            e.HasIndex(v => new { v.SurahNum, v.AyahNum }).IsUnique();
        });

        modelBuilder.Entity<Translation>(e =>
        {
            e.ToTable("translations");
            e.HasKey(t => t.Id);
            e.Property(t => t.VerseId).HasColumnName("verse_id");
            e.Property(t => t.Language).HasColumnName("language").HasMaxLength(10);
            e.Property(t => t.Text).HasColumnName("text");
            e.Property(t => t.Translator).HasColumnName("translator").HasMaxLength(200);
            e.HasOne(t => t.Verse).WithMany(v => v.Translations).HasForeignKey(t => t.VerseId);
        });

        modelBuilder.Entity<Tafsir>(e =>
        {
            e.ToTable("tafsir");
            e.HasKey(t => t.Id);
            e.Property(t => t.VerseId).HasColumnName("verse_id");
            e.Property(t => t.Source).HasColumnName("source").HasMaxLength(200);
            e.Property(t => t.Content).HasColumnName("content");
            e.HasOne(t => t.Verse).WithMany(v => v.Tafsirs).HasForeignKey(t => t.VerseId);
        });
    }
}
