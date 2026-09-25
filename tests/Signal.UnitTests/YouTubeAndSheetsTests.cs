using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Signal.Application.Services;
using Signal.Domain.Entities;
using Signal.Domain.Enums;
using Signal.Infrastructure.Persistence;
using Signal.Infrastructure.Providers.YouTube;
using Xunit;

namespace Signal.UnitTests;

public class YouTubeAndSheetsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SignalDbContext _dbContext;
    private readonly GoogleSheetApprovalService _sheetsService;

    public YouTubeAndSheetsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SignalDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new SignalDbContext(options);
        _dbContext.Database.EnsureCreated();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GOOGLE_SHEETS_ENABLED"] = "false", // Test local offline queueing
                ["GOOGLE_APPS_SCRIPT_URL"] = ""
            })
            .Build();

        _sheetsService = new GoogleSheetApprovalService(
            _dbContext,
            new HttpClient(),
            config,
            NullLogger<GoogleSheetApprovalService>.Instance);
    }

    [Fact]
    public void CalculateWatchScore_FavorsTechnicalTutorialsOverHype()
    {
        // Technical tutorial (15 min)
        var techScore = YouTubeSourceProvider.CalculateWatchScore(
            "Building an AI Agent with .NET 10 and MCP Architecture",
            "In this deep dive tutorial we implement an agent with code walkthrough",
            TimeSpan.FromMinutes(15));

        // Clickbait clip (1 min)
        var baitScore = YouTubeSourceProvider.CalculateWatchScore(
            "YOU WON'T BELIEVE THIS SECRET TRICK! GAME OVER FOR CODERS!",
            "Shocking new AI tool revealed comment ai for access",
            TimeSpan.FromMinutes(1));

        // Assert: High quality tutorial scored significantly higher than bait
        Assert.True(techScore > 0.8, $"Expected techScore > 0.8, got {techScore}");
        Assert.True(baitScore < 0.4, $"Expected baitScore < 0.4, got {baitScore}");
    }

    [Fact]
    public async Task SyncOpportunityAsync_WhenOffline_EnqueuesRecordInDatabase()
    {
        // Arrange
        var opp = new Opportunity
        {
            Title = "Claude Pro Free Trial",
            OpportunityType = OpportunityType.FreeTrial,
            Value = "$20",
            OfficialSourceUrl = "https://anthropic.com/pro-trial",
            Status = OpportunityStatus.PendingReview
        };
        _dbContext.Opportunities.Add(opp);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sheetsService.SyncOpportunityAsync(opp);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(OpportunityStatus.Approved, opp.Status);

        var queuedRecord = await _dbContext.GoogleSheetRecords
            .FirstOrDefaultAsync(r => r.OpportunityId == opp.Id);

        Assert.NotNull(queuedRecord);
        Assert.Equal(SyncStatus.PendingSync, queuedRecord.Status);
        Assert.Contains("Claude Pro Free Trial", queuedRecord.PayloadJson);
    }

    [Fact]
    public async Task InspectUrlAsync_DirectGitHubRepo_ExtractsGitUrlAndDetectsTopic()
    {
        var service = new Signal.Infrastructure.Providers.Video.VideoInspectionService(new HttpClient(), NullLogger<Signal.Infrastructure.Providers.Video.VideoInspectionService>.Instance);
        var result = await service.InspectUrlAsync("https://github.com/vllm-project/vllm");

        Assert.Equal("https://github.com/vllm-project/vllm", result.ExtractedGitHubUrl);
        Assert.NotNull(result.ExtractedTopic);
        Assert.True(result.IsLegit);
        Assert.Equal("GitHub", result.Platform);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
