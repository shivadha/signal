using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Signal.Application.Services;
using Signal.Domain.Entities;
using Signal.Domain.Enums;
using Signal.Infrastructure.Persistence;
using Signal.Infrastructure.Providers.AI;
using Signal.Infrastructure.Providers.Telegram;
using Xunit;

namespace Signal.UnitTests;

public class TelegramBotTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SignalDbContext _dbContext;
    private readonly TelegramBotService _botService;

    public TelegramBotTests()
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
                ["TELEGRAM_BOT_TOKEN"] = "test-token",
                ["TELEGRAM_CHAT_ID"] = "123456"
            })
            .Build();

        var httpClient = new HttpClient();
        var normalizer = new ContentNormalizationService();
        var aiProvider = new NoneAiProvider();

        _botService = new TelegramBotService(
            httpClient,
            config,
            NullLogger<TelegramBotService>.Instance,
            normalizer,
            aiProvider);
    }

    [Fact]
    public async Task HandleCommandAsync_StartCommand_ReturnsWelcomeMessage()
    {
        // Act
        var reply = await _botService.HandleCommandAsync("/start", _dbContext);

        // Assert
        Assert.Contains("Welcome to SIGNAL", reply);
        Assert.Contains("/today", reply);
        Assert.Contains("/check", reply);
    }

    [Fact]
    public async Task HandleCommandAsync_CheckUnseenUrl_IdentifiesNetNew()
    {
        // Act
        var reply = await _botService.HandleCommandAsync("/check https://anthropic.com/claude-3-7-sonnet", _dbContext);

        // Assert
        Assert.Contains("SIGNAL CHECK", reply);
        Assert.Contains("Already Seen:</b> NO", reply);
        Assert.Contains("Net-new link", reply);
    }

    [Fact]
    public async Task HandleCommandAsync_CheckExistingUrl_DetectsDuplicate()
    {
        // Arrange
        var source = new Source
        {
            Name = "OpenAI Blog",
            Url = "https://openai.com",
            SourceType = SourceType.OfficialBlog
        };
        _dbContext.Sources.Add(source);
        await _dbContext.SaveChangesAsync();

        var contentItem = new ContentItem
        {
            SourceId = source.Id,
            Platform = "Blog",
            Title = "OpenAI Announces GPT-5",
            Url = "https://openai.com/news/gpt-5",
            CanonicalUrl = "https://openai.com/news/gpt-5",
            ContentHash = "hash123",
            PublishedAt = DateTimeOffset.UtcNow
        };
        _dbContext.ContentItems.Add(contentItem);
        await _dbContext.SaveChangesAsync();

        // Act
        var reply = await _botService.HandleCommandAsync("/check https://openai.com/news/gpt-5?utm_source=twitter", _dbContext);

        // Assert
        Assert.Contains("SIGNAL CHECK", reply);
        Assert.Contains("Already Seen:</b> YES", reply);
        Assert.Contains("No new notification required", reply);
    }


    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
