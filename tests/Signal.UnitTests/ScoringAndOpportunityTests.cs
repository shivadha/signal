using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Signal.Application.Services;
using Signal.Domain.Entities;
using Signal.Domain.Enums;
using Signal.Infrastructure.Persistence;
using Signal.Infrastructure.Providers.AI;
using Xunit;

namespace Signal.UnitTests;

public class ScoringAndOpportunityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SignalDbContext _dbContext;
    private readonly SignalScoringService _scoringService;
    private readonly OpportunityDetectionService _opportunityService;

    public ScoringAndOpportunityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SignalDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new SignalDbContext(options);
        _dbContext.Database.EnsureCreated();

        _scoringService = new SignalScoringService();
        _opportunityService = new OpportunityDetectionService(
            _dbContext,
            new NoneAiProvider(),
            NullLogger<OpportunityDetectionService>.Instance);
    }

    [Fact]
    public void CalculateScore_AppliesSourceQualityAndEvidenceWeights()
    {
        // Arrange
        var source = new Source
        {
            Name = "OpenAI Blog",
            Url = "https://openai.com",
            TrustTier = TrustTier.Tier1_Official,
            IsOfficial = true
        };

        var item = new ContentItem
        {
            Title = "Introducing OpenAI Swarm",
            PublishedAt = DateTimeOffset.UtcNow
        };

        // Act
        var score = _scoringService.CalculateScore(
            item,
            source,
            aiRelevance: 0.9,
            rageBaitScore: 0.0,
            isOpportunity: false,
            expiryDate: null);

        // Assert
        Assert.True(score.FinalScore >= 0.70, $"Expected final score >= 0.70, but got {score.FinalScore}");
        Assert.Equal(1.0, score.SourceQualityScore);
        Assert.Equal(1.0, score.EvidenceScore);
    }

    [Fact]
    public void CalculateScore_AppliesRageBaitAndDuplicatePenalties()
    {
        // Arrange
        var source = new Source
        {
            Name = "Community Aggregator",
            Url = "https://reddit.com/r/ai",
            TrustTier = TrustTier.Tier4_Community,
            IsOfficial = false
        };

        var item = new ContentItem
        {
            Title = "YOU WON'T BELIEVE THIS SECRET TRICK",
            PublishedAt = DateTimeOffset.UtcNow,
            IsDuplicate = true
        };

        // Act
        var score = _scoringService.CalculateScore(
            item,
            source,
            aiRelevance: 0.6,
            rageBaitScore: 1.0,
            isOpportunity: false,
            expiryDate: null);

        // Assert
        Assert.True(score.FinalScore < 0.20, $"Expected penalized score < 0.20, but got {score.FinalScore}");
        Assert.True(score.RageBaitPenalty > 0);
        Assert.True(score.DuplicatePenalty > 0);
    }

    [Fact]
    public async Task EvaluateAndCreateOpportunityAsync_CreatesValidOpportunityEntity()
    {
        // Arrange
        var source = new Source
        {
            Name = "Anthropic Blog",
            Url = "https://anthropic.com",
            TrustTier = TrustTier.Tier1_Official
        };
        _dbContext.Sources.Add(source);
        await _dbContext.SaveChangesAsync();

        var item = new ContentItem
        {
            SourceId = source.Id,
            Platform = "Blog",
            Title = "Anthropic announces free credits for developer accounts until Dec 31 2026",
            Url = "https://anthropic.com/free-credits",
            CanonicalUrl = "https://anthropic.com/free-credits",
            ContentHash = "hash999",
            PublishedAt = DateTimeOffset.UtcNow
        };
        _dbContext.ContentItems.Add(item);
        await _dbContext.SaveChangesAsync();

        // Act
        var opp = await _opportunityService.EvaluateAndCreateOpportunityAsync(item);

        // Assert
        Assert.NotNull(opp);
        Assert.Equal(OpportunityType.FreeApiCredits, opp.OpportunityType);
        Assert.True(opp.IsFree);
        Assert.NotNull(opp.ExpiryDate);
        Assert.Equal(VerificationStatus.Verified, opp.VerificationStatus);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
