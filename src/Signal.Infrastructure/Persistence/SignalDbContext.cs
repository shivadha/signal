using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Signal.Application.Common.Interfaces;
using Signal.Domain.Entities;

namespace Signal.Infrastructure.Persistence;

public class DateTimeOffsetToUnixConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToUnixConverter() : base(
        v => v.ToUnixTimeMilliseconds(),
        v => DateTimeOffset.FromUnixTimeMilliseconds(v))
    {
    }
}

public class NullableDateTimeOffsetToUnixConverter : ValueConverter<DateTimeOffset?, long?>
{
    public NullableDateTimeOffsetToUnixConverter() : base(
        v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null)
    {
    }
}

public class SignalDbContext : DbContext, ISignalDbContext
{
    public SignalDbContext(DbContextOptions<SignalDbContext> options) : base(options)
    {
    }

    public DbSet<Source> Sources => Set<Source>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<VideoContent> VideoContents => Set<VideoContent>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<StoryContent> StoryContents => Set<StoryContent>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<GoogleSheetRecord> GoogleSheetRecords => Set<GoogleSheetRecord>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<UserFeedback> UserFeedbacks => Set<UserFeedback>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Map DateTimeOffset to Unix milliseconds for full SQLite ORDER BY and indexing support
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToUnixConverter>();

        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<NullableDateTimeOffsetToUnixConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Source Configuration
        modelBuilder.Entity<Source>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).HasMaxLength(150).IsRequired();
            entity.Property(s => s.Url).HasMaxLength(500).IsRequired();
            entity.Property(s => s.FeedUrl).HasMaxLength(500);
            entity.Property(s => s.Category).HasMaxLength(100);
            entity.Property(s => s.Language).HasMaxLength(10);
            entity.Property(s => s.Country).HasMaxLength(10);
            entity.HasIndex(s => s.IsActive);
        });

        // ContentItem Configuration
        modelBuilder.Entity<ContentItem>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Platform).HasMaxLength(50).IsRequired();
            entity.Property(c => c.Title).HasMaxLength(500).IsRequired();
            entity.Property(c => c.Url).HasMaxLength(1000).IsRequired();
            entity.Property(c => c.CanonicalUrl).HasMaxLength(1000).IsRequired();
            entity.Property(c => c.ContentHash).HasMaxLength(64).IsRequired();
            entity.Property(c => c.Author).HasMaxLength(150);
            entity.Property(c => c.Language).HasMaxLength(10);
            entity.Property(c => c.Category).HasMaxLength(100);

            entity.HasIndex(c => c.Url);
            entity.HasIndex(c => c.CanonicalUrl);
            entity.HasIndex(c => c.ContentHash);
            entity.HasIndex(c => c.PublishedAt);

            entity.HasOne(c => c.Source)
                .WithMany(s => s.ContentItems)
                .HasForeignKey(c => c.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.DuplicateOf)
                .WithMany()
                .HasForeignKey(c => c.DuplicateOfId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // VideoContent Configuration
        modelBuilder.Entity<VideoContent>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Platform).HasMaxLength(50).IsRequired();
            entity.Property(v => v.VideoId).HasMaxLength(100).IsRequired();
            entity.Property(v => v.Title).HasMaxLength(500).IsRequired();
            entity.Property(v => v.ChannelId).HasMaxLength(100);
            entity.Property(v => v.ChannelName).HasMaxLength(200);
            entity.Property(v => v.ThumbnailUrl).HasMaxLength(1000);
            entity.Property(v => v.CaptionLanguage).HasMaxLength(10);

            entity.HasIndex(v => v.VideoId);

            entity.HasOne(v => v.ContentItem)
                .WithOne(c => c.VideoContent)
                .HasForeignKey<VideoContent>(v => v.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Story Configuration
        modelBuilder.Entity<Story>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.CanonicalTopic).HasMaxLength(250).IsRequired();
            entity.Property(s => s.Title).HasMaxLength(500).IsRequired();
            entity.Property(s => s.Category).HasMaxLength(100);
            entity.Property(s => s.Status).HasMaxLength(50);

            entity.HasIndex(s => s.CanonicalTopic);
            entity.HasIndex(s => s.FirstDetectedAt);
        });

        // StoryContent Configuration
        modelBuilder.Entity<StoryContent>(entity =>
        {
            entity.HasKey(sc => sc.Id);
            entity.Property(sc => sc.Platform).HasMaxLength(50);

            entity.HasOne(sc => sc.Story)
                .WithMany(s => s.StoryContents)
                .HasForeignKey(sc => sc.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sc => sc.ContentItem)
                .WithMany(c => c.StoryContents)
                .HasForeignKey(sc => sc.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Opportunity Configuration
        modelBuilder.Entity<Opportunity>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Title).HasMaxLength(500).IsRequired();
            entity.Property(o => o.Value).HasMaxLength(100);
            entity.Property(o => o.Currency).HasMaxLength(10);
            entity.Property(o => o.Country).HasMaxLength(100);
            entity.Property(o => o.Eligibility).HasMaxLength(500);
            entity.Property(o => o.OfficialSourceUrl).HasMaxLength(1000);

            entity.HasIndex(o => o.ExpiryDate);
            entity.HasIndex(o => o.Status);

            entity.HasOne(o => o.ContentItem)
                .WithMany(c => c.Opportunities)
                .HasForeignKey(o => o.ContentItemId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(o => o.Story)
                .WithMany(s => s.Opportunities)
                .HasForeignKey(o => o.StoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // GoogleSheetRecord Configuration
        modelBuilder.Entity<GoogleSheetRecord>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.SheetName).HasMaxLength(100);
            entity.Property(g => g.ErrorMessage).HasMaxLength(1000);

            entity.HasIndex(g => g.Status);

            entity.HasOne(g => g.Opportunity)
                .WithMany(o => o.GoogleSheetRecords)
                .HasForeignKey(g => g.OpportunityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserPreference Configuration
        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(up => up.Id);
            entity.Property(up => up.Key).HasMaxLength(100).IsRequired();
            entity.Property(up => up.Category).HasMaxLength(100);
            entity.HasIndex(up => up.Key).IsUnique();
        });

        // UserFeedback Configuration
        modelBuilder.Entity<UserFeedback>(entity =>
        {
            entity.HasKey(uf => uf.Id);
            entity.Property(uf => uf.FeedbackType).HasMaxLength(50).IsRequired();

            entity.HasOne(uf => uf.ContentItem)
                .WithMany(c => c.FeedbackItems)
                .HasForeignKey(uf => uf.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
