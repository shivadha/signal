namespace Signal.Domain.Entities;

public class UserPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty; // e.g. "AI Coding", "Claude", "Free Tools"
    public int Weight { get; set; } = 10;           // 1 to 10 scale
    public string Category { get; set; } = "General";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class UserFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ContentItemId { get; set; }
    public ContentItem? ContentItem { get; set; }

    public Guid? StoryId { get; set; }
    public Story? Story { get; set; }

    public string FeedbackType { get; set; } = "Useful"; // Useful, NotUseful, Saved, MuteSource, MuteTopic
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
