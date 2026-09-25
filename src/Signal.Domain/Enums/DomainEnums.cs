namespace Signal.Domain.Enums;

public enum SourceType
{
    Rss = 1,
    OfficialBlog = 2,
    YouTubeChannel = 3,
    Reddit = 4,
    TelegramChannel = 5,
    Social = 6
}

public enum TrustTier
{
    Tier1_Official = 1,      // Primary official source (OpenAI, Microsoft, Google, etc.)
    Tier2_Established = 2,   // Reputable technical publications
    Tier3_Specialist = 3,    // Domain expert blogs & creators
    Tier4_Community = 4,     // Reddit, forums, social aggregators
    Tier5_Unknown = 5        // Unverified / discovery leads
}

public enum OpportunityType
{
    FreeTool = 1,
    FreeSubscription = 2,
    FreeTrial = 3,
    FreeApiCredits = 4,
    FreeCloudCredits = 5,
    DeveloperProgram = 6,
    StudentProgram = 7,
    FreeCourse = 8,
    Discount = 9,
    LimitedTimeOffer = 10,
    NewFreeTier = 11,
    OpenSourceRelease = 12,
    Other = 99
}

public enum VerificationStatus
{
    Discovered = 1,
    Unverified = 2,
    PartiallyVerified = 3,
    Verified = 4,
    Contradicted = 5,
    Expired = 6,
    Unknown = 7
}

public enum OpportunityStatus
{
    Discovered = 1,
    PendingReview = 2,
    Approved = 3,
    Rejected = 4,
    Saved = 5,
    Claimed = 6,
    Expired = 7,
    Archived = 8
}

public enum RelationshipType
{
    Primary = 1,
    Coverage = 2,
    Commentary = 3,
    Repost = 4,
    Duplicate = 5,
    Related = 6,
    Contradictory = 7
}

public enum SyncStatus
{
    PendingSync = 1,
    Synced = 2,
    Failed = 3,
    Skipped = 4
}
