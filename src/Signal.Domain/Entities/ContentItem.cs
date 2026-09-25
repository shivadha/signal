namespace Signal.Domain.Entities;

public class ContentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;

    public string Platform { get; set; } = "RSS";
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string CanonicalUrl { get; set; } = string.Empty;
    public string? Author { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;
    public string ContentHash { get; set; } = string.Empty; // SHA-256
    public string? TextContent { get; set; }
    public string Language { get; set; } = "en";
    public string? Category { get; set; }
    public string? Summary { get; set; }
    public string? ImageUrl { get; set; }

    public bool IsDuplicate { get; set; } = false;
    public Guid? DuplicateOfId { get; set; }
    public ContentItem? DuplicateOf { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public VideoContent? VideoContent { get; set; }
    public ICollection<StoryContent> StoryContents { get; set; } = new List<StoryContent>();
    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public ICollection<UserFeedback> FeedbackItems { get; set; } = new List<UserFeedback>();
}
