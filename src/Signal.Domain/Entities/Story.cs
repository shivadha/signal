using Signal.Domain.Enums;

namespace Signal.Domain.Entities;

public class Story
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CanonicalTopic { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Category { get; set; } = "AI";
    public DateTimeOffset FirstDetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public double ImportanceScore { get; set; } = 0.5;
    public double RelevanceScore { get; set; } = 0.5;
    public double EvidenceScore { get; set; } = 0.5;
    public double FreshnessScore { get; set; } = 1.0;

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Unverified;
    public string Status { get; set; } = "Active";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<StoryContent> StoryContents { get; set; } = new List<StoryContent>();
    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
}

public class StoryContent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryId { get; set; }
    public Story Story { get; set; } = null!;

    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;

    public string Platform { get; set; } = string.Empty;
    public RelationshipType RelationshipType { get; set; } = RelationshipType.Coverage;
    public double SimilarityScore { get; set; } = 1.0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
