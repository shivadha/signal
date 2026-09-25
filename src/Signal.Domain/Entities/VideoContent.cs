namespace Signal.Domain.Entities;

public class VideoContent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;

    public string Platform { get; set; } = "YouTube";
    public string VideoId { get; set; } = string.Empty;
    public string? ChannelId { get; set; }
    public string? ChannelName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string? Transcript { get; set; }
    public bool TranscriptAvailable { get; set; } = false;
    public string? CaptionLanguage { get; set; }
    public long ViewCount { get; set; } = 0;
    public long LikeCount { get; set; } = 0;
    public long CommentCount { get; set; } = 0;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
