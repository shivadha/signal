using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;
using Signal.Application.Services;
using Signal.Domain.Entities;
using Signal.Infrastructure.Providers.Telegram;

namespace Signal.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Worker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(30);
    private bool _isFirstRun = true;

    private static readonly Regex GitHubRepoRegex = new(
        @"https?://(?:www\.)?github\.com/([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Worker(IServiceProvider serviceProvider, ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Signal Ingestion Worker starting. Polling interval: {Interval}", _pollingInterval);

        // Allow app host to stabilize
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Executing scheduled source ingestion pass at: {Time}", DateTimeOffset.UtcNow);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var ingestionService = scope.ServiceProvider.GetRequiredService<SourceIngestionService>();
                    var opportunityDetector = scope.ServiceProvider.GetRequiredService<OpportunityDetectionService>();
                    var botService = scope.ServiceProvider.GetRequiredService<TelegramBotService>();
                    var translationService = scope.ServiceProvider.GetRequiredService<ITranslationService>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ISignalDbContext>();

                    // On startup, ensure any existing database records in foreign languages are translated
                    if (_isFirstRun)
                    {
                        await EnsureExistingContentTranslatedAsync(dbContext, translationService, stoppingToken);
                    }

                    var summary = await ingestionService.IngestAllActiveSourcesAsync(stoppingToken);

                    _logger.LogInformation(
                        "Ingestion complete. Checked: {Checked}, Discovered: {Discovered}, New: {New}, Duplicates: {Dupes}",
                        summary.SourcesChecked,
                        summary.ItemsDiscovered,
                        summary.NewItemsSaved,
                        summary.DuplicatesFiltered);

                    if (summary.Errors.Count > 0)
                    {
                        foreach (var err in summary.Errors)
                        {
                            _logger.LogWarning("Ingestion warning/error: {Error}", err);
                        }
                    }

                    // Process automatic notification for newly discovered items
                    if (summary.NewItems.Count > 0)
                    {
                        await ProcessNewDiscoveriesNotificationAsync(summary.NewItems, opportunityDetector, botService, translationService, stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("Zero new items in scheduled pass. Noise filtered: 0 alerts sent.");
                    }

                    _isFirstRun = false;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Signal ingestion worker loop.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Signal Ingestion Worker stopped gracefully.");
    }

    private async Task EnsureExistingContentTranslatedAsync(ISignalDbContext dbContext, ITranslationService translationService, CancellationToken ct)
    {
        try
        {
            var candidates = await dbContext.ContentItems
                .Where(c => c.Language != "en")
                .Take(100)
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                candidates = (await dbContext.ContentItems
                    .OrderByDescending(c => c.DiscoveredAt)
                    .Take(150)
                    .ToListAsync(ct))
                    .Where(c => translationService.NeedsTranslation(c.Title) || translationService.NeedsTranslation(c.Summary ?? ""))
                    .Take(100)
                    .ToList();
            }

            if (candidates.Count > 0)
            {
                _logger.LogInformation("Translating {Count} existing international items in database to English...", candidates.Count);
                int translatedCount = 0;
                foreach (var item in candidates)
                {
                    bool changed = false;
                    if (translationService.NeedsTranslation(item.Title))
                    {
                        var trans = await translationService.TranslateToEnglishAsync(item.Title, item.Language, ct);
                        item.Title = translationService.SanitizeToEnglish(trans);
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(item.Summary) && translationService.NeedsTranslation(item.Summary))
                    {
                        var trans = await translationService.TranslateToEnglishAsync(item.Summary, item.Language, ct);
                        item.Summary = translationService.SanitizeToEnglish(trans);
                        changed = true;
                    }

                    if (changed || item.Language != "en")
                    {
                        item.Language = "en";
                        translatedCount++;
                    }
                }

                if (translatedCount > 0)
                {
                    await dbContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Successfully translated and saved {Count} items to English in database.", translatedCount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to run translation sweep on existing items.");
        }
    }

    private async Task ProcessNewDiscoveriesNotificationAsync(
        List<ContentItem> newItems,
        OpportunityDetectionService opportunityDetector,
        TelegramBotService botService,
        ITranslationService translationService,
        CancellationToken ct)
    {
        var handledItemIds = new HashSet<Guid>();

        // 1. Detect and dispatch high-value developer opportunities
        int oppAlertsSent = 0;
        foreach (var item in newItems)
        {
            try
            {
                var opp = await opportunityDetector.EvaluateAndCreateOpportunityAsync(item, ct);
                if (opp != null)
                {
                    handledItemIds.Add(item.Id);
                    if (oppAlertsSent < 3) // Cap at 3 per cycle to prevent notification fatigue
                    {
                        await botService.SendOpportunityCardAsync(opp, ct);
                        oppAlertsSent++;
                        await Task.Delay(500, ct); // Rate limiting
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate opportunity for {Title}", item.Title);
            }
        }

        // 2. Detect and dispatch developer tools & GitHub repos
        int toolAlertsSent = 0;
        var toolCandidates = newItems
            .Where(it => !handledItemIds.Contains(it.Id))
            .Where(it => it.Url.Contains("github.com")
                      || it.Platform == "GitHub"
                      || (it.Category != null && it.Category.Contains("Tool"))
                      || it.Title.ToLower().Contains("cli")
                      || it.Title.ToLower().Contains("library")
                      || it.Title.ToLower().Contains("open-source"))
            .ToList();

        foreach (var tool in toolCandidates)
        {
            handledItemIds.Add(tool.Id);
            if (toolAlertsSent < 2) // Cap at 2 tool cards per cycle
            {
                await botService.SendToolCardAsync(tool, ct);
                toolAlertsSent++;
                await Task.Delay(500, ct);
            }
        }

        // 3. Dispatch Topic-Segregated Digest for remaining discoveries
        var remainingItems = newItems
            .Where(it => !handledItemIds.Contains(it.Id))
            .OrderByDescending(it => it.PublishedAt)
            .ToList();

        if (remainingItems.Count > 0)
        {
            var aiNews = remainingItems.Where(it => IsAiNews(it)).Take(3).ToList();
            var aiItemIds = new HashSet<Guid>(aiNews.Select(x => x.Id));

            var toolsAndSoftware = remainingItems.Where(it => !aiItemIds.Contains(it.Id) && IsSoftwareOrTool(it)).Take(3).ToList();
            var toolItemIds = new HashSet<Guid>(toolsAndSoftware.Select(x => x.Id));

            var freebiesAndDeals = remainingItems.Where(it => !aiItemIds.Contains(it.Id) && !toolItemIds.Contains(it.Id) && IsFreebieOrDeal(it)).Take(3).ToList();
            var freebieItemIds = new HashSet<Guid>(freebiesAndDeals.Select(x => x.Id));

            var generalTech = remainingItems.Where(it => !aiItemIds.Contains(it.Id) && !toolItemIds.Contains(it.Id) && !freebieItemIds.Contains(it.Id)).Take(3).ToList();

            // Find first item with image for rich banner preview
            var bannerImage = remainingItems.FirstOrDefault(it => !string.IsNullOrWhiteSpace(it.ImageUrl))?.ImageUrl;
            var imagePrefix = !string.IsNullOrWhiteSpace(bannerImage) ? $"<a href=\"{bannerImage}\">&#8205;</a>" : string.Empty;

            var header = _isFirstRun
                ? $"{imagePrefix}📡 <b>SIGNAL — STARTUP INTELLIGENCE BRIEFING</b>\n" +
                  $"<i>Ingested {newItems.Count} new item(s). Segregated by topic with brief context:</i>\n\n"
                : $"{imagePrefix}📡 <b>SIGNAL — 30-MINUTE DISCOVERY UPDATE</b>\n" +
                  $"<i>Found {newItems.Count} new update(s) in this 30-minute pass:</i>\n\n";

            var sb = new StringBuilder(header);

            if (aiNews.Count > 0)
            {
                sb.AppendLine("🤖 <b>AI NEWS & OFFERS</b>");
                foreach (var it in aiNews)
                {
                    await AppendItemDetailsAsync(sb, it, translationService, ct);
                }
                sb.AppendLine();
            }

            if (toolsAndSoftware.Count > 0)
            {
                sb.AppendLine("🛠️ <b>SOFTWARE & DEVELOPER TOOLS</b>");
                foreach (var it in toolsAndSoftware)
                {
                    await AppendItemDetailsAsync(sb, it, translationService, ct);
                }
                sb.AppendLine();
            }

            if (freebiesAndDeals.Count > 0)
            {
                sb.AppendLine("🎁 <b>FREEBIES & DEALS</b>");
                foreach (var it in freebiesAndDeals)
                {
                    await AppendItemDetailsAsync(sb, it, translationService, ct);
                }
                sb.AppendLine();
            }

            if (generalTech.Count > 0)
            {
                sb.AppendLine("📰 <b>TECH ECOSYSTEM & INNOVATION</b>");
                foreach (var it in generalTech)
                {
                    await AppendItemDetailsAsync(sb, it, translationService, ct);
                }
                sb.AppendLine();
            }

            var message = sb.ToString();
            message = translationService.SanitizeToEnglish(message);

            if (message.Length > 3900)
            {
                message = message[..3890] + "...";
            }

            await botService.SendAlertAsync(message, ct);
        }
    }

    private static async Task AppendItemDetailsAsync(StringBuilder sb, ContentItem it, ITranslationService translationService, CancellationToken ct)
    {
        var title = it.Title;
        if (translationService.NeedsTranslation(title))
        {
            title = await translationService.TranslateToEnglishAsync(title, it.Language, ct);
        }
        title = translationService.SanitizeToEnglish(title);

        var briefContext = !string.IsNullOrWhiteSpace(it.Summary) ? it.Summary : it.TextContent;
        if (!string.IsNullOrWhiteSpace(briefContext) && translationService.NeedsTranslation(briefContext))
        {
            briefContext = await translationService.TranslateToEnglishAsync(briefContext, it.Language, ct);
        }
        briefContext = translationService.SanitizeToEnglish(briefContext);

        if (!string.IsNullOrWhiteSpace(briefContext) && briefContext.Length > 160)
        {
            briefContext = briefContext[..157] + "...";
        }

        var imageTag = !string.IsNullOrWhiteSpace(it.ImageUrl) ? $" • <a href=\"{it.ImageUrl}\">🖼️ Preview</a>" : "";
        sb.AppendLine($"• <b><a href=\"{it.Url}\">{WebUtility.HtmlEncode(title)}</a></b>{imageTag}");
        sb.AppendLine($"  📍 <i>{WebUtility.HtmlEncode(it.Platform)}</i> | 🏷️ <i>{WebUtility.HtmlEncode(it.Category ?? "Tech")}</i>");
        if (!string.IsNullOrWhiteSpace(briefContext))
        {
            sb.AppendLine($"  💡 <i>{WebUtility.HtmlEncode(briefContext)}</i>");
        }

        var git = ExtractGitHubRepoUrl($"{it.Url} {it.Summary} {it.TextContent}");
        if (!string.IsNullOrWhiteSpace(git))
        {
            sb.AppendLine($"  🐙 <b>GitHub:</b> <a href=\"{git}\">{git}</a>");
        }
    }

    private static string? ExtractGitHubRepoUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = GitHubRepoRegex.Match(text);
        if (match.Success)
        {
            var owner = match.Groups[1].Value.ToLowerInvariant();
            var repo = match.Groups[2].Value.ToLowerInvariant().TrimEnd('/', '.');
            string[] reserved = ["features", "pricing", "about", "contact", "login", "signup", "settings", "explore", "trending", "topics", "pulls", "issues", "site"];
            if (!reserved.Contains(owner) && !reserved.Contains(repo))
            {
                return $"https://github.com/{match.Groups[1].Value}/{match.Groups[2].Value.TrimEnd('/', '.')}";
            }
        }
        return null;
    }

    private static bool IsAiNews(ContentItem it)
    {
        var text = $"{it.Title} {it.Category} {it.Summary}".ToLowerInvariant();
        return text.Contains("ai") || text.Contains("llm") || text.Contains("model") || text.Contains("openai")
            || text.Contains("anthropic") || text.Contains("deepseek") || text.Contains("qwen")
            || text.Contains("gpt") || text.Contains("gemini") || text.Contains("claude");
    }

    private static bool IsSoftwareOrTool(ContentItem it)
    {
        var text = $"{it.Title} {it.Category} {it.Url}".ToLowerInvariant();
        return it.Url.Contains("github.com") || it.Platform == "GitHub" || text.Contains("tool")
            || text.Contains("software") || text.Contains("cli") || text.Contains("library")
            || text.Contains("open-source") || text.Contains("framework");
    }

    private static bool IsFreebieOrDeal(ContentItem it)
    {
        var text = $"{it.Title} {it.Category}".ToLowerInvariant();
        return text.Contains("free") || text.Contains("deal") || text.Contains("discount")
            || text.Contains("credit") || text.Contains("grant") || text.Contains("trial")
            || text.Contains("freebies");
    }
}
