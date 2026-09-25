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
using Signal.Infrastructure.Providers.Translation;
using Signal.Infrastructure.Providers.Video;
using Xunit;

namespace Signal.UnitTests;

public class TranslationAndStrictEnglishTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SignalDbContext _dbContext;
    private readonly TelegramBotService _botService;
    private readonly FreeTranslationService _translationService;

    public TranslationAndStrictEnglishTests()
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
        var videoInspector = new VideoInspectionService(httpClient, NullLogger<VideoInspectionService>.Instance);
        _translationService = new FreeTranslationService(httpClient, NullLogger<FreeTranslationService>.Instance);

        _botService = new TelegramBotService(
            httpClient,
            config,
            NullLogger<TelegramBotService>.Instance,
            normalizer,
            aiProvider,
            videoInspector,
            _translationService);
    }

    [Fact]
    public void NeedsTranslation_DetectsForeignScriptsCorrectly()
    {
        Assert.True(_translationService.NeedsTranslation("「作って」ではなく「約束」を書く：Claude Codeに渡した構築プロンプトの設計図"));
        Assert.True(_translationService.NeedsTranslation("深度求索发布最新开源模型"));
        Assert.True(_translationService.NeedsTranslation("새로운 AI 도구 출시"));
        Assert.False(_translationService.NeedsTranslation("OpenAI announces GPT-5 and new Agent SDK"));
    }

    [Fact]
    public void DetectLanguage_IdentifiesScripts()
    {
        Assert.Equal("ja", _translationService.DetectLanguage("Claude Codeの「ついでに」を1件ずつ確認制にした話"));
        Assert.Equal("zh-CN", _translationService.DetectLanguage("阿里开源千问新模型"));
        Assert.Equal("ko", _translationService.DetectLanguage("인공지능 개발자 도구"));
        Assert.Equal("en", _translationService.DetectLanguage("Fast API Gateway"));
    }

    [Fact]
    public void SanitizeToEnglish_StripsResidualNonLatinCharacters()
    {
        var mixed = "Claude Code 開発ツール (Developer Tool)";
        var sanitized = _translationService.SanitizeToEnglish(mixed);
        Assert.False(_translationService.NeedsTranslation(sanitized));
        Assert.Contains("Claude Code", sanitized);
        Assert.Contains("Developer Tool", sanitized);
    }

    [Fact]
    public async Task TranslateToEnglishAsync_TranslatesJapaneseAndChinese()
    {
        var japanese = "今日の市場ブリーフィング";
        var result = await _translationService.TranslateToEnglishAsync(japanese);
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.False(_translationService.NeedsTranslation(result));

        var chinese = "开源项目发布";
        var resultZh = await _translationService.TranslateToEnglishAsync(chinese);
        Assert.False(string.IsNullOrWhiteSpace(resultZh));
        Assert.False(_translationService.NeedsTranslation(resultZh));
    }

    [Fact]
    public async Task TelegramCommands_GuaranteeZeroForeignCharactersInOutput()
    {
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Name = "Zenn Japan",
            Url = "https://zenn.dev",
            FeedUrl = "https://zenn.dev/feed",
            SourceType = SourceType.Rss,
            Category = "Japanese AI & Tech",
            Language = "ja",
            Country = "JP",
            TrustTier = TrustTier.Tier3_Specialist,
            IsOfficial = false,
            IsActive = true
        };
        _dbContext.Sources.Add(source);
        await _dbContext.SaveChangesAsync();

        // Add foreign language item into database
        var item = new ContentItem
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            Title = "「作って」ではなく「約束」を書く：Claude Codeに渡した構築プロンプトの設計図",
            Url = "https://zenn.dev/sample-jp",
            CanonicalUrl = "https://zenn.dev/sample-jp",
            Platform = "Zenn",
            Category = "Japanese AI & Tech",
            Summary = "Claude Codeプロンプト設計に関する技術記事",
            DiscoveredAt = DateTimeOffset.UtcNow,
            PublishedAt = DateTimeOffset.UtcNow,
            Language = "ja",
            ContentHash = "hash-jp-1"
        };
        _dbContext.ContentItems.Add(item);
        await _dbContext.SaveChangesAsync();

        // 1. Test /today command
        var todayResp = await _botService.HandleCommandAsync("/today", _dbContext);
        var todayText = todayResp.Text;
        Assert.False(_translationService.NeedsTranslation(todayText), "Telegram /today output contained non-English foreign characters!");

        // 2. Test /global command
        var globalResp = await _botService.HandleCommandAsync("/global", _dbContext);
        var globalText = globalResp.Text;
        Assert.False(_translationService.NeedsTranslation(globalText), "Telegram /global output contained non-English foreign characters!");
    }

    public void Dispose()
    {
        _connection.Dispose();
        _dbContext.Dispose();
    }
}
