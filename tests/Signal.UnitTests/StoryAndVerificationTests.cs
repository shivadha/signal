using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Signal.Application.Services;
using Signal.Domain.Entities;
using Signal.Domain.Enums;
using Signal.Infrastructure.Persistence;
using Xunit;

namespace Signal.UnitTests;

public class StoryAndVerificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SignalDbContext _dbContext;
    private readonly StoryClusteringService _clusteringService;
    private readonly VerificationService _verificationService;

    public StoryAndVerificationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SignalDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new SignalDbContext(options);
        _dbContext.Database.EnsureCreated();

        _clusteringService = new StoryClusteringService(
            _dbContext,
            NullLogger<StoryClusteringService>.Instance);

        _verificationService = new VerificationService(
            _dbContext,
            NullLogger<VerificationService>.Instance);
    }

    [Fact]
    public async Task ClusterContentItemAsync_DifferentHeadlinesSameTopic_ClustersIntoSingleStory()
    {
        var source = new Source
        {
            Name = "Test Source",
            Url = "https://example.com",
            SourceType = SourceType.OfficialBlog
        };
        _dbContext.Sources.Add(source);
        await _dbContext.SaveChangesAsync();

        // Item 1: YouTube video headline
        var item1 = new ContentItem
        {
            SourceId = source.Id,
            Platform = "YouTube",
            Title = "OpenAI releases Swarm multi agent orchestrator framework",
            PublishedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };
        _dbContext.ContentItems.Add(item1);
        await _dbContext.SaveChangesAsync();

        var cluster1 = await _clusteringService.ClusterContentItemAsync(item1);
        Assert.True(cluster1.IsNewStory);

        // Item 2: Article headline about same event
        var item2 = new ContentItem
        {
            SourceId = source.Id,
            Platform = "Blog",
            Title = "OpenAI Swarm: New multi agent framework for developers",
            PublishedAt = DateTimeOffset.UtcNow
        };
        _dbContext.ContentItems.Add(item2);
        await _dbContext.SaveChangesAsync();

        var cluster2 = await _clusteringService.ClusterContentItemAsync(item2);


        // Assert: Both are grouped into the same story!
        Assert.False(cluster2.IsNewStory);
        Assert.Equal(cluster1.Story.Id, cluster2.Story.Id);

        var story = await _dbContext.Stories
            .Include(s => s.StoryContents)
            .FirstOrDefaultAsync(s => s.Id == cluster1.Story.Id);

        Assert.NotNull(story);
        Assert.Equal(2, story.StoryContents.Count);
    }

    [Fact]
    public async Task VerifyOpportunityAsync_DetectsExpiredOpportunity()
    {
        // Arrange
        var opp = new Opportunity
        {
            Title = "Expired Summer Promo",
            ExpiryDate = DateTimeOffset.UtcNow.AddDays(-2),
            OfficialSourceUrl = "https://openai.com/expired",
            Status = OpportunityStatus.Discovered
        };
        _dbContext.Opportunities.Add(opp);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _verificationService.VerifyOpportunityAsync(opp);

        // Assert
        Assert.True(result.IsExpired);
        Assert.Equal(VerificationStatus.Expired, result.Status);
        Assert.Equal(OpportunityStatus.Expired, opp.Status);
    }

    [Fact]
    public async Task VerifyOpportunityAsync_ConfirmsOfficialPublisherDomain()
    {
        // Arrange
        var opp = new Opportunity
        {
            Title = "GitHub Copilot for Students",
            ExpiryDate = DateTimeOffset.UtcNow.AddMonths(3),
            OfficialSourceUrl = "https://github.com/education/students",
            Status = OpportunityStatus.Discovered
        };
        _dbContext.Opportunities.Add(opp);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _verificationService.VerifyOpportunityAsync(opp);

        // Assert
        Assert.False(result.IsExpired);
        Assert.Equal(VerificationStatus.Verified, result.Status);
        Assert.True(result.ConfidenceScore >= 0.90);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
