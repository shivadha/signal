using Signal.Domain.Enums;

namespace Signal.Domain.Entities;

public class Opportunity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ContentItemId { get; set; }
    public ContentItem? ContentItem { get; set; }

    public Guid? StoryId { get; set; }
    public Story? Story { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public OpportunityType OpportunityType { get; set; } = OpportunityType.FreeTool;
    public string? Value { get; set; }
    public string? Currency { get; set; } = "USD";
    public bool IsFree { get; set; } = true;
    public string? Eligibility { get; set; }
    public string? Country { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }
    public string? ClaimMethod { get; set; }
    public string? OfficialSourceUrl { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Discovered;
    public double VerificationScore { get; set; } = 0.5;
    public double RelevanceScore { get; set; } = 0.5;
    public double UrgencyScore { get; set; } = 0.0;
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Discovered;

    public DateTimeOffset? LastVerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<GoogleSheetRecord> GoogleSheetRecords { get; set; } = new List<GoogleSheetRecord>();
}

public class GoogleSheetRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    public Guid? StoryId { get; set; }
    public Story? Story { get; set; }

    public int? RowIndex { get; set; }
    public string SheetName { get; set; } = "Opportunities";
    public string PayloadJson { get; set; } = "{}";
    public SyncStatus Status { get; set; } = SyncStatus.PendingSync;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTimeOffset? SyncedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
