using Signal.Domain.Enums;

namespace Signal.Domain.Entities;

public class Source
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? FeedUrl { get; set; }
    public SourceType SourceType { get; set; } = SourceType.Rss;
    public string Category { get; set; } = "AI";
    public TrustTier TrustTier { get; set; } = TrustTier.Tier1_Official;
    public bool IsOfficial { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string Language { get; set; } = "en";
    public string? Country { get; set; }
    public DateTimeOffset? LastFetchedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ContentItem> ContentItems { get; set; } = new List<ContentItem>();
}
