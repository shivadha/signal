using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Signal.Application.Services;
using Signal.Domain.Entities;
using Signal.Domain.Enums;
using Signal.Infrastructure.Data;
using Signal.Infrastructure.Persistence;
using Xunit;

namespace Signal.IntegrationTests;

public class DatabaseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SignalDbContext _dbContext;
    private readonly ContentNormalizationService _normalizer;
    private readonly DuplicateDetectionService _duplicateDetector;

    public DatabaseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SignalDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new SignalDbContext(options);
        _dbContext.Database.EnsureCreated();

        _normalizer = new ContentNormalizationService();
        _duplicateDetector = new DuplicateDetectionService(_dbContext);
    }

    [Fact]
    public async Task SourceSeeder_SeedsDefaultSourcesAndPreferences()
    {
        // Act
        await SourceSeeder.SeedAsync(_dbContext, NullLogger.Instance);

        // Assert
        var sources = await _dbContext.Sources.ToListAsync();
        var preferences = await _dbContext.UserPreferences.ToListAsync();

        Assert.NotEmpty(sources);
        Assert.Contains(sources, s => s.Name == "OpenAI News");
        Assert.Contains(sources, s => s.Name == "GitHub Blog");
        Assert.NotEmpty(preferences);
        Assert.Contains(preferences, p => p.Key == "AI Coding");
    }

    [Fact]
    public async Task DuplicateDetector_IdentifiesExactAndCanonicalMatches()
    {
        // Arrange
        var source = new Source
        {
            Name = "Test Source",
            Url = "https://example.com",
            SourceType = SourceType.OfficialBlog
        };
        _dbContext.Sources.Add(source);
        await _dbContext.SaveChangesAsync();

        var item = new ContentItem
        {
            SourceId = source.Id,
            Platform = "Blog",
            Title = "OpenAI Announces GPT-5",
            Url = "https://example.com/gpt5",
            CanonicalUrl = "https://example.com/gpt5",
            ContentHash = _normalizer.ComputeContentHash("OpenAI Announces GPT-5", "full content"),
            PublishedAt = DateTimeOffset.UtcNow
        };
        _dbContext.ContentItems.Add(item);
        await _dbContext.SaveChangesAsync();

        // Act 1: Exact URL duplicate check
        var exactResult = await _duplicateDetector.CheckDuplicateAsync(
            "https://example.com/gpt5",
            "https://example.com/gpt5",
            "dummy_hash");

        // Act 2: Canonical URL duplicate check (with tracking params)
        var dirtyUrl = "https://example.com/gpt5?utm_source=twitter&fbclid=xyz";
        var canonicalUrl = _normalizer.CanonicalizeUrl(dirtyUrl);
        var canonicalResult = await _duplicateDetector.CheckDuplicateAsync(
            dirtyUrl,
            canonicalUrl,
            "dummy_hash");

        // Act 3: Content hash collision check (different url, same text)
        var hashResult = await _duplicateDetector.CheckDuplicateAsync(
            "https://mirror.example.com/story",
            "https://mirror.example.com/story",
            item.ContentHash);

        // Assert
        Assert.True(exactResult.IsDuplicate);
        Assert.Equal("Exact URL match", exactResult.Reason);

        Assert.True(canonicalResult.IsDuplicate);
        Assert.Equal("Canonical URL match (tracking parameters removed)", canonicalResult.Reason);

        Assert.True(hashResult.IsDuplicate);
        Assert.Equal("Content SHA-256 hash collision", hashResult.Reason);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
