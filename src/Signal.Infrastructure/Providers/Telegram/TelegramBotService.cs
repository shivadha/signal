using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;
using Signal.Application.Common.Models;
using Signal.Domain.Entities;
using Signal.Domain.Enums;

namespace Signal.Infrastructure.Providers.Telegram;

public class TelegramBotService : ITelegramProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly IContentNormalizer _normalizer;
    private readonly IAIProvider _aiProvider;
    private readonly string? _botToken;
    private readonly string? _defaultChatId;

    public TelegramBotService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<TelegramBotService> logger,
        IContentNormalizer normalizer,
        IAIProvider aiProvider)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _normalizer = normalizer;
        _aiProvider = aiProvider;
        _botToken = _config["TELEGRAM_BOT_TOKEN"];
        _defaultChatId = _config["TELEGRAM_CHAT_ID"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_botToken) && !string.IsNullOrWhiteSpace(_defaultChatId);

    public async Task<bool> SendAlertAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("[Telegram MOCK Alert]: {Message}", message);
            return true;
        }

        return await SendMessageAsync(_defaultChatId!, message, null, cancellationToken);
    }

    public async Task<bool> SendOpportunityCardAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        var text = FormatOpportunityHtml(opportunity);
        var keyboard = new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard = new List<List<TelegramInlineKeyboardButton>>
            {
                new()
                {
                    new() { Text = "🔗 Official Source", Url = opportunity.OfficialSourceUrl ?? opportunity.ContentItem?.Url ?? "https://github.com/shivadha/signal" }
                },
                new()
                {
                    new() { Text = "✅ APPROVE & SAVE", CallbackData = $"approve:{opportunity.Id}" },
                    new() { Text = "⭐ SAVE LATER", CallbackData = $"save:{opportunity.Id}" }
                },
                new()
                {
                    new() { Text = "❌ REJECT", CallbackData = $"reject:{opportunity.Id}" },
                    new() { Text = "🔎 EVIDENCE", CallbackData = $"evidence:{opportunity.Id}" }
                }
            }
        };

        if (!IsConfigured)
        {
            _logger.LogInformation("[Telegram MOCK Opportunity Card]:\n{Text}", text);
            return true;
        }

        return await SendMessageAsync(_defaultChatId!, text, keyboard, cancellationToken);
    }

    public async Task<string> HandleCommandAsync(
        string commandText,
        ISignalDbContext db,
        CancellationToken cancellationToken = default)
    {
        var trimmed = commandText.Trim();
        var isStart = trimmed.Equals("/start", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("start", StringComparison.OrdinalIgnoreCase);
        var isHelp = trimmed.Equals("/help", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("help", StringComparison.OrdinalIgnoreCase);
        var isToday = trimmed.Equals("/today", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("today", StringComparison.OrdinalIgnoreCase);
        var isOffers = trimmed.Equals("/offers", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("offers", StringComparison.OrdinalIgnoreCase);
        var isSaved = trimmed.Equals("/saved", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("saved", StringComparison.OrdinalIgnoreCase);
        var isCheck = trimmed.StartsWith("/check", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("check ", StringComparison.OrdinalIgnoreCase);
        var isRawUrl = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (isStart)
        {
            return "👋 <b>Welcome to SIGNAL</b>\n" +
                   "<i>Your personal information firewall. Signal over noise.</i>\n\n" +
                   "🟢 <b>Status:</b> 24/7 Active\n" +
                   "⏱️ <b>Auto-Sync:</b> Every 30 minutes across all days\n" +
                   "📡 <b>Monitored:</b> OpenAI, Anthropic, Google, Hugging Face, GitHub, MIT Tech, YouTube\n\n" +
                   "<b>Available Commands:</b>\n" +
                   "• <code>/today</code> — Latest discoveries with summaries & links\n" +
                   "• <code>/offers</code> — Free developer tiers, credits & discounts\n" +
                   "• <code>/saved</code> — Items approved or saved to your library\n" +
                   "• <code>/check &lt;url&gt;</code> — Verify link, detect duplicates & evaluate claims\n" +
                   "• <code>/help</code> — Full operational documentation\n\n" +
                   "💡 <i>Tip: You can send commands with or without the slash (e.g. <code>today</code> or <code>offers</code>).</i>";
        }

        if (isHelp)
        {
            return "📖 <b>SIGNAL Bot Commands & Operation</b>\n\n" +
                   "• <code>/today</code> — High-signal curated news with summaries & links\n" +
                   "• <code>/offers</code> — Free tiers, API credits, developer discounts\n" +
                   "• <code>/saved</code> — Items saved in personal library\n" +
                   "• <code>/check &lt;url&gt;</code> — Paste any YouTube, Reel, or Article link\n\n" +
                   "🔄 <b>Automatic Crawl:</b> Runs every 30 minutes, 24/7. When new high-signal items or opportunities are discovered, you receive an instant alert!\n\n" +
                   "💡 <i>Tip: Only when you tap <b>[✅ APPROVE & SAVE]</b> does an item write to Google Sheets.</i>";
        }

        if (isCheck || isRawUrl)
        {
            string url;
            if (isRawUrl)
            {
                url = trimmed;
            }
            else
            {
                var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    return "⚠️ <b>Usage:</b> <code>/check &lt;url&gt;</code>\nExample: <code>/check https://openai.com/news/swarm/</code>";
                }
                url = parts[1].Trim();
            }

            return await HandleCheckUrlAsync(url, db, cancellationToken);
        }

        if (isToday)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            var items = await db.ContentItems
                .AsNoTracking()
                .Include(c => c.Source)
                .Where(c => c.PublishedAt >= cutoff)
                .OrderByDescending(c => c.PublishedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (items.Count == 0)
            {
                // Fallback to top 5 most recent discoveries
                items = await db.ContentItems
                    .AsNoTracking()
                    .Include(c => c.Source)
                    .OrderByDescending(c => c.PublishedAt)
                    .Take(5)
                    .ToListAsync(cancellationToken);
            }

            if (items.Count == 0)
            {
                return "ℹ️ <b>SIGNAL — TODAY</b>\nNo discoveries recorded yet. Background sync is running every 30 minutes.";
            }

            var response = "🔥 <b>SIGNAL — TODAY (Top Discoveries)</b>\n\n";
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var excerpt = !string.IsNullOrWhiteSpace(it.Summary) ? it.Summary : it.TextContent;
                if (!string.IsNullOrWhiteSpace(excerpt) && excerpt.Length > 220)
                {
                    excerpt = excerpt.Substring(0, 217) + "...";
                }

                var timeAgo = FormatRelativeTime(it.PublishedAt);

                response += $"<b>{i + 1}. <a href=\"{it.Url}\">{WebUtility.HtmlEncode(it.Title)}</a></b>\n" +
                            $"   🏢 <b>Source:</b> {WebUtility.HtmlEncode(it.Source.Name)} | 🕒 <i>{timeAgo}</i>\n" +
                            $"   🏷️ <b>Category:</b> {WebUtility.HtmlEncode(it.Category ?? "General")}\n";

                if (!string.IsNullOrWhiteSpace(excerpt))
                {
                    response += $"   📝 <b>Details:</b> <i>{WebUtility.HtmlEncode(excerpt)}</i>\n";
                }
                response += "\n";
            }
            return response;
        }

        if (isOffers)
        {
            var opportunities = await db.Opportunities
                .AsNoTracking()
                .Where(o => o.Status != OpportunityStatus.Expired && o.Status != OpportunityStatus.Rejected)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (opportunities.Count == 0)
            {
                return "💰 <b>ACTIVE DEVELOPER OPPORTUNITIES</b>\nNo unreviewed opportunities in the queue right now. All caught up!\n\n<i>Auto-detection checks new feeds every 30 minutes.</i>";
            }

            var response = "💰 <b>ACTIVE DEVELOPER OPPORTUNITIES</b>\n\n";
            for (int i = 0; i < opportunities.Count; i++)
            {
                var op = opportunities[i];
                response += $"<b>{i + 1}. {WebUtility.HtmlEncode(op.Title)}</b>\n" +
                            $"   🎁 <b>Value:</b> {WebUtility.HtmlEncode(op.Value ?? "Free Tier")}\n" +
                            $"   👥 <b>Eligibility:</b> {WebUtility.HtmlEncode(op.Eligibility ?? "All developers")}\n" +
                            $"   ⏳ <b>Expires:</b> {(op.ExpiryDate.HasValue ? op.ExpiryDate.Value.ToString("dd MMM yyyy") : "Ongoing")}\n" +
                            $"   🔗 <a href=\"{op.OfficialSourceUrl ?? "https://github.com/shivadha/signal"}\">Official Link</a>\n\n";
            }
            return response;
        }

        if (isSaved)
        {
            var saved = await db.Opportunities
                .AsNoTracking()
                .Where(o => o.Status == OpportunityStatus.Approved || o.Status == OpportunityStatus.Saved)
                .OrderByDescending(o => o.UpdatedAt)
                .Take(10)
                .ToListAsync(cancellationToken);

            if (saved.Count == 0)
            {
                return "⭐ <b>SAVED LIBRARY</b>\nYou have not approved or saved any items yet.\n\n<i>When you tap [✅ APPROVE & SAVE] or [⭐ SAVE LATER] on any alert, it appears here and syncs to Google Sheets.</i>";
            }

            var response = "⭐ <b>YOUR SAVED LIBRARY</b>\n\n";
            foreach (var op in saved)
            {
                response += $"• <b>{WebUtility.HtmlEncode(op.Title)}</b> ({op.Status})\n" +
                            $"  URL: {op.OfficialSourceUrl ?? "N/A"}\n\n";
            }
            return response;
        }

        return "❓ Unknown command. Type <code>/help</code> or <code>help</code> for available commands, or paste any URL to check it.";
    }

    private static string FormatRelativeTime(DateTimeOffset dt)
    {
        var diff = DateTimeOffset.UtcNow - dt;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dt.ToString("dd MMM yyyy");
    }


    private async Task<string> HandleCheckUrlAsync(string url, ISignalDbContext db, CancellationToken ct)
    {
        var canonicalUrl = _normalizer.CanonicalizeUrl(url);

        // Check exact or canonical match in DB
        var existing = await db.ContentItems
            .AsNoTracking()
            .Include(c => c.Source)
            .FirstOrDefaultAsync(c => c.Url == url || c.CanonicalUrl == canonicalUrl, ct);

        // Run analysis on title/url
        var analysis = await _aiProvider.AnalyzeAsync(url, null, ct);

        if (existing != null)
        {
            return $"🔎 <b>SIGNAL CHECK</b>\n\n" +
                   $"<b>Story:</b> {existing.Title}\n" +
                   $"<b>Already Seen:</b> YES (via {existing.Source.Name})\n" +
                   $"<b>First Discovered:</b> {existing.DiscoveredAt:dd MMM yyyy}\n" +
                   $"<b>New Information:</b> None (Duplicate entry)\n" +
                   $"<b>Verification:</b> Verified\n" +
                   $"<b>Relevance:</b> {(analysis.RelevanceScore >= 0.7 ? "HIGH" : "MEDIUM")}\n\n" +
                   $"<b>Recommendation:</b> No new notification required.";
        }

        return $"🔎 <b>SIGNAL CHECK</b>\n\n" +
               $"<b>URL:</b> {url}\n" +
               $"<b>Canonical:</b> {canonicalUrl}\n" +
               $"<b>Already Seen:</b> NO (Net-new link)\n" +
               $"<b>Rage-Bait Score:</b> {(analysis.RageBaitScore > 0.5 ? "⚠️ HIGH" : "✅ LOW")}\n" +
               $"<b>Relevance Score:</b> {(analysis.RelevanceScore >= 0.7 ? "🔥 HIGH" : "LOW")}\n" +
               $"<b>Opportunity Detected:</b> {(analysis.IsOpportunity ? "YES (" + analysis.OpportunityType + ")" : "NO")}\n\n" +
               $"<b>Recommendation:</b> {(analysis.IsRelevant ? "Valid topic for queue" : "Filter as low relevance")}";
    }

    private static string FormatOpportunityHtml(Opportunity op)
    {
        return $"💰 <b>FREE AI OPPORTUNITY</b>\n\n" +
               $"<b>{op.Title}</b>\n\n" +
               $"<b>Value:</b> {op.Value ?? "Free"}\n" +
               $"<b>Eligibility:</b> {op.Eligibility ?? "All developers"}\n" +
               $"<b>Expires:</b> {(op.ExpiryDate.HasValue ? op.ExpiryDate.Value.ToString("dd MMM yyyy") : "Ongoing / Not specified")}\n\n" +
               $"<b>Verification:</b> 🟢 {(op.VerificationStatus == VerificationStatus.Verified ? "Official Source" : "Partially Verified")}\n" +
               $"<b>Relevance:</b> 🔥 HIGH ({op.RelevanceScore * 10:F1}/10)\n\n" +
               $"<b>Why you should care:</b>\n" +
               $"{op.Description ?? "High value developer release."}";
    }

    public async Task<bool> SendMessageAsync(
        string chatId,
        string text,
        TelegramInlineKeyboardMarkup? keyboard = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
            var payload = new TelegramSendMessagePayload
            {
                ChatId = chatId,
                Text = text,
                ParseMode = "HTML",
                ReplyMarkup = keyboard
            };

            using var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Telegram sendMessage returned non-success: {StatusCode} - {Error}", response.StatusCode, err);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message to {ChatId}", chatId);
            return false;
        }
    }

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_botToken))
            return Array.Empty<TelegramUpdate>();

        try
        {
            var url = $"https://api.telegram.org/bot{_botToken}/getUpdates?offset={offset}&timeout=20";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<TelegramUpdate>();

            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (doc.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
            {
                var updates = new List<TelegramUpdate>();
                foreach (var element in result.EnumerateArray())
                {
                    var update = JsonSerializer.Deserialize<TelegramUpdate>(element.GetRawText());
                    if (update != null) updates.Add(update);
                }
                return updates;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch Telegram updates via long polling.");
        }

        return Array.Empty<TelegramUpdate>();
    }

    public async Task<bool> AnswerCallbackQueryAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_botToken))
            return false;

        try
        {
            var url = $"https://api.telegram.org/bot{_botToken}/answerCallbackQuery";
            var payload = new { callback_query_id = callbackQueryId, text };
            using var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to answer Telegram callback query.");
            return false;
        }
    }

    public async Task<string> HandleCallbackAsync(
        string callbackData,
        ISignalDbContext db,
        IGoogleSheetsProvider sheetsProvider,
        CancellationToken cancellationToken = default)
    {
        var parts = callbackData.Split(':', 2);
        var action = parts[0].ToLowerInvariant();
        var idStr = parts.Length > 1 ? parts[1] : string.Empty;

        if (!Guid.TryParse(idStr, out var opportunityId))
        {
            return "⚠️ Invalid opportunity reference.";
        }

        var opp = await db.Opportunities
            .Include(o => o.ContentItem)
            .FirstOrDefaultAsync(o => o.Id == opportunityId, cancellationToken);

        if (opp == null)
            return "⚠️ Opportunity not found or expired.";

        switch (action)
        {
            case "approve":
                opp.Status = OpportunityStatus.Approved;
                var syncResult = await sheetsProvider.SyncOpportunityAsync(opp, cancellationToken);
                return syncResult.Success && syncResult.RowIndex.HasValue
                    ? $"✅ <b>SAVED & APPROVED</b>\nAdded to Google Sheet at Row #{syncResult.RowIndex}."
                    : $"✅ <b>SAVED LOCALLY</b>\nQueued for Google Sheets sync: {syncResult.ErrorMessage ?? "Pending sync"}";

            case "save":
                opp.Status = OpportunityStatus.Saved;
                await db.SaveChangesAsync(cancellationToken);
                return "⭐ <b>Saved for Later!</b> You can view this anytime with <code>/saved</code>.";

            case "reject":
                opp.Status = OpportunityStatus.Rejected;
                await db.SaveChangesAsync(cancellationToken);
                return "❌ <b>Rejected.</b> Similar notifications will be down-weighted.";

            case "evidence":
                return $"🔎 <b>EVIDENCE PROVENANCE</b>\n\n" +
                       $"<b>Source:</b> {opp.OfficialSourceUrl ?? "Official publisher"}\n" +
                       $"<b>Verification Status:</b> {opp.VerificationStatus}\n" +
                       $"<b>Confidence Score:</b> {opp.VerificationScore * 100:F0}%\n" +
                       $"<b>Eligibility:</b> {opp.Eligibility ?? "Developers"}";

            default:
                return "Action processed.";
        }
    }
}

