using System.Net;
using System.Text;
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
                        await ProcessNewDiscoveriesNotificationAsync(summary.NewItems, opportunityDetector, botService, stoppingToken);
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

    private async Task ProcessNewDiscoveriesNotificationAsync(
        List<ContentItem> newItems,
        OpportunityDetectionService opportunityDetector,
        TelegramBotService botService,
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
                sb.AppendLine("🤖 <b>AI RESEARCH & FRONTIER NEWS</b>");
                foreach (var it in aiNews)
                {
                    AppendItemDetails(sb, it);
                }
                sb.AppendLine();
            }

            if (toolsAndSoftware.Count > 0)
            {
                sb.AppendLine("🛠️ <b>SOFTWARE & DEVELOPER TOOLS</b>");
                foreach (var it in toolsAndSoftware)
                {
                    AppendItemDetails(sb, it);
                }
                sb.AppendLine();
            }

            if (freebiesAndDeals.Count > 0)
            {
                sb.AppendLine("🎁 <b>FREEBIES, DEALS & SUBSCRIPTIONS</b>");
                foreach (var it in freebiesAndDeals)
                {
                    AppendItemDetails(sb, it);
                }
                sb.AppendLine();
            }

            if (generalTech.Count > 0)
            {
                sb.AppendLine("📰 <b>TECH ECOSYSTEM & INNOVATION</b>");
                foreach (var it in generalTech)
                {
                    AppendItemDetails(sb, it);
                }
                sb.AppendLine();
            }

            var message = sb.ToString();
            if (message.Length > 3900)
            {
                message = message[..3890] + "...";
            }

            await botService.SendAlertAsync(message, ct);
        }
    }

    private static void AppendItemDetails(StringBuilder sb, ContentItem it)
    {
        var briefContext = !string.IsNullOrWhiteSpace(it.Summary) ? it.Summary : it.TextContent;
        if (!string.IsNullOrWhiteSpace(briefContext) && briefContext.Length > 160)
        {
            briefContext = briefContext[..157] + "...";
        }

        sb.AppendLine($"• <b><a href=\"{it.Url}\">{WebUtility.HtmlEncode(it.Title)}</a></b>");
        sb.AppendLine($"  📍 <i>{WebUtility.HtmlEncode(it.Platform)}</i> | 🏷️ <i>{WebUtility.HtmlEncode(it.Category ?? "Tech")}</i>");
        if (!string.IsNullOrWhiteSpace(briefContext))
        {
            sb.AppendLine($"  💡 <i>{WebUtility.HtmlEncode(briefContext)}</i>");
        }
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

