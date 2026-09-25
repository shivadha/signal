using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;
using Signal.Domain.Entities;
using Signal.Domain.Enums;

namespace Signal.Application.Services;

public record ClusteringResult
{
    public Story Story { get; init; } = null!;
    public bool IsNewStory { get; init; }
    public bool HasNewInformation { get; init; }
    public List<string> NewInformationItems { get; init; } = new();
}

public class StoryClusteringService
{
    private readonly ISignalDbContext _dbContext;
    private readonly ILogger<StoryClusteringService> _logger;

    public StoryClusteringService(ISignalDbContext dbContext, ILogger<StoryClusteringService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ClusteringResult> ClusterContentItemAsync(
        ContentItem item,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-14);
        var recentStories = await _dbContext.Stories
            .Include(s => s.StoryContents)
            .ThenInclude(sc => sc.ContentItem)
            .Where(s => s.FirstDetectedAt >= cutoff && s.Status == "Active")
            .ToListAsync(cancellationToken);

        var itemTokens = Tokenize(item.Title);

        Story? bestMatchStory = null;
        double bestSimilarity = 0.0;

        foreach (var story in recentStories)
        {
            var storyTokens = Tokenize(story.Title);
            var similarity = ComputeJaccardSimilarity(itemTokens, storyTokens);

            if (similarity > bestSimilarity && similarity >= 0.40) // 40% token overlap threshold
            {
                bestSimilarity = similarity;
                bestMatchStory = story;
            }
        }

        if (bestMatchStory != null)
        {
            // Cluster into existing story
            var relationship = bestSimilarity >= 0.85 ? RelationshipType.Duplicate : RelationshipType.Coverage;
            var link = new StoryContent
            {
                StoryId = bestMatchStory.Id,
                ContentItemId = item.Id,
                Platform = item.Platform,
                RelationshipType = relationship,
                SimilarityScore = Math.Round(bestSimilarity, 2),
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.StoryContents.Add(link);

            // Check for new information delta
            var (hasNewInfo, newInfoItems) = DetectNewInformation(bestMatchStory, item);
            if (hasNewInfo)
            {
                bestMatchStory.LastUpdatedAt = DateTimeOffset.UtcNow;
                _logger.LogInformation("Story '{Title}' updated with net-new info: {Items}",
                    bestMatchStory.Title, string.Join(", ", newInfoItems));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new ClusteringResult
            {
                Story = bestMatchStory,
                IsNewStory = false,
                HasNewInformation = hasNewInfo,
                NewInformationItems = newInfoItems
            };
        }

        // Create net-new Story
        var newStory = new Story
        {
            CanonicalTopic = ExtractCanonicalTopic(item.Title),
            Title = item.Title,
            Summary = item.Summary ?? item.TextContent,
            Category = item.Category ?? "AI",
            FirstDetectedAt = item.PublishedAt,
            LastUpdatedAt = item.PublishedAt,
            ImportanceScore = 0.7,
            RelevanceScore = 0.8,
            EvidenceScore = 0.85,
            FreshnessScore = 1.0,
            VerificationStatus = VerificationStatus.Verified,
            Status = "Active"
        };

        _dbContext.Stories.Add(newStory);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var primaryLink = new StoryContent
        {
            StoryId = newStory.Id,
            ContentItemId = item.Id,
            Platform = item.Platform,
            RelationshipType = RelationshipType.Primary,
            SimilarityScore = 1.0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.StoryContents.Add(primaryLink);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new Story cluster: {Title}", newStory.Title);

        return new ClusteringResult
        {
            Story = newStory,
            IsNewStory = true,
            HasNewInformation = true,
            NewInformationItems = new List<string> { "Initial discovery" }
        };
    }

    private static (bool hasNewInfo, List<string> items) DetectNewInformation(Story story, ContentItem newItem)
    {
        var existingText = $"{story.Title} {story.Summary}".ToLowerInvariant();
        var newText = $"{newItem.Title} {newItem.TextContent}".ToLowerInvariant();

        var deltaKeywords = new[] { "pricing", "api", "benchmark", "weights", "download", "released", "paper", "free tier", "open source" };
        var detectedDeltas = new List<string>();

        foreach (var keyword in deltaKeywords)
        {
            if (newText.Contains(keyword) && !existingText.Contains(keyword))
            {
                detectedDeltas.Add($"New details regarding {keyword}");
            }
        }

        return (detectedDeltas.Count > 0, detectedDeltas);
    }

    private static string ExtractCanonicalTopic(string title)
    {
        var clean = title.Split(new[] { '—', '-', ':', '|' }, 2)[0].Trim();
        return clean.Length > 200 ? clean[..200] : clean;
    }

    private static HashSet<string> Tokenize(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "but", "is", "are", "of", "to", "in", "for", "with", "on", "at", "by", "this", "that"
        };

        return text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '-', '_', '/', '\\', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToHashSet();
    }

    private static double ComputeJaccardSimilarity(HashSet<string> s1, HashSet<string> s2)
    {
        if (s1.Count == 0 && s2.Count == 0) return 1.0;
        if (s1.Count == 0 || s2.Count == 0) return 0.0;

        var intersection = s1.Intersect(s2).Count();
        var union = s1.Union(s2).Count();

        return (double)intersection / union;
    }
}
