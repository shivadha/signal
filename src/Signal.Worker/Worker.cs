using System.Net;
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
        var oppContentItemIds = new HashSet<Guid>();

        // 1. Detect and dispatch high-value developer opportunities
        int oppAlertsSent = 0;
        foreach (var item in newItems)
        {
            try
            {
                var opp = await opportunityDetector.EvaluateAndCreateOpportunityAsync(item, ct);
                if (opp != null)
                {
                    oppContentItemIds.Add(item.Id);
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

        // 2. Dispatch High-Signal Digest for non-opportunity discoveries
        var nonOppItems = newItems
            .Where(it => !oppContentItemIds.Contains(it.Id))
            .OrderByDescending(it => it.PublishedAt)
            .Take(5)
            .ToList();

        if (nonOppItems.Count > 0)
        {
            var header = _isFirstRun
                ? "📡 <b>SIGNAL — STARTUP INTELLIGENCE BRIEFING</b>\n" +
                  $"<i>Ingested {newItems.Count} new item(s). Showing top {nonOppItems.Count} discoveries:</i>\n\n"
                : "📡 <b>SIGNAL — 30-MINUTE DISCOVERY UPDATE</b>\n" +
                  $"<i>Found {newItems.Count} new update(s) in this 30-minute pass:</i>\n\n";

            var message = header;
            for (int i = 0; i < nonOppItems.Count; i++)
            {
                var it = nonOppItems[i];
                var excerpt = !string.IsNullOrWhiteSpace(it.Summary) ? it.Summary : it.TextContent;
                if (!string.IsNullOrWhiteSpace(excerpt) && excerpt.Length > 180)
                {
                    excerpt = excerpt.Substring(0, 177) + "...";
                }

                message += $"<b>{i + 1}. <a href=\"{it.Url}\">{WebUtility.HtmlEncode(it.Title)}</a></b>\n" +
                           $"   📍 <i>{WebUtility.HtmlEncode(it.Platform)}</i> | 🏷️ <i>{WebUtility.HtmlEncode(it.Category ?? "General")}</i>\n";

                if (!string.IsNullOrWhiteSpace(excerpt))
                {
                    message += $"   📝 <i>{WebUtility.HtmlEncode(excerpt)}</i>\n";
                }
                message += "\n";
            }

            await botService.SendAlertAsync(message, ct);
        }
    }
}

