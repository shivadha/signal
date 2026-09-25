using System.Net;
using System.Net.Http.Json;
using System.Text;
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
    private readonly IVideoInspectionService _videoInspector;
    private readonly ITranslationService _translationService;
    private readonly string? _botToken;
    private readonly string? _defaultChatId;

    public TelegramBotService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<TelegramBotService> logger,
        IContentNormalizer normalizer,
        IAIProvider aiProvider,
        IVideoInspectionService videoInspector,
        ITranslationService translationService)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _normalizer = normalizer;
        _aiProvider = aiProvider;
        _videoInspector = videoInspector;
        _translationService = translationService;
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
        var imageUrl = opportunity.ContentItem?.ImageUrl;
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            text = $"<a href=\"{imageUrl}\">&#8205;</a>" + text;
        }

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

    public async Task<bool> SendToolCardAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        var imagePrefix = !string.IsNullOrWhiteSpace(item.ImageUrl)
            ? $"<a href=\"{item.ImageUrl}\">&#8205;</a>"
            : string.Empty;

        var text = $"{imagePrefix}🛠️ <b>NEW DEVELOPER TOOL / REPOSITORY</b>\n\n" +
                   $"<b><a href=\"{item.Url}\">{WebUtility.HtmlEncode(item.Title)}</a></b>\n\n" +
                   $"📍 <b>Platform:</b> {WebUtility.HtmlEncode(item.Platform)}\n" +
                   $"🏷️ <b>Category:</b> {WebUtility.HtmlEncode(item.Category ?? "Developer Tools")}\n\n" +
                   $"💡 <b>What it is & Why Useful:</b>\n" +
                   $"{WebUtility.HtmlEncode(item.Summary ?? item.TextContent ?? "Productivity accelerator.")}";

        var keyboard = new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard = new List<List<TelegramInlineKeyboardButton>>
            {
                new()
                {
                    new() { Text = "🔗 Open Repository / Link", Url = item.Url }
                },
                new()
                {
                    new() { Text = "⭐ SAVE TO GOOGLE SHEET (TOOLS)", CallbackData = $"savetool:{item.Id}" }
                }
            }
        };

        if (!IsConfigured)
        {
            _logger.LogInformation("[Telegram MOCK Tool Card]:\n{Text}", text);
            return true;
        }

        return await SendMessageAsync(_defaultChatId!, text, keyboard, cancellationToken);
    }

    public static TelegramReplyKeyboardMarkup CreateMainNavigationKeyboard() => new()
    {
        Keyboard = new List<List<TelegramKeyboardButton>>
        {
            new()
            {
                new() { Text = "🔥 Today" },
                new() { Text = "💰 Offers" },
                new() { Text = "🛠️ Tools" }
            },
            new()
            {
                new() { Text = "🌏 Global AI" },
                new() { Text = "🛡️ Verify URL" },
                new() { Text = "📖 Help" }
            }
        },
        ResizeKeyboard = true,
        IsPersistent = true
    };

    public async Task<TelegramReplyResult> HandleCommandAsync(
        string commandText,
        ISignalDbContext db,
        CancellationToken cancellationToken = default)
    {
        var trimmed = commandText.Trim();
        var isStart = trimmed.Equals("/start", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("start", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("/menu", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("menu", StringComparison.OrdinalIgnoreCase);
        var isHelp = trimmed.Equals("/help", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("help", StringComparison.OrdinalIgnoreCase)
                  || trimmed.Equals("📖 help", StringComparison.OrdinalIgnoreCase);
        var isToday = trimmed.Equals("/today", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("today", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("🔥 today", StringComparison.OrdinalIgnoreCase);
        var isOffers = trimmed.Equals("/offers", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("offers", StringComparison.OrdinalIgnoreCase)
                    || trimmed.Equals("offer", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("💰 offers", StringComparison.OrdinalIgnoreCase);
        var isTools = trimmed.Equals("/tools", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("tools", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("🛠️ tools", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("/github", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("github", StringComparison.OrdinalIgnoreCase);
        var isGlobal = trimmed.Equals("/global", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("global", StringComparison.OrdinalIgnoreCase)
                    || trimmed.Equals("/intl", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("intl", StringComparison.OrdinalIgnoreCase)
                    || trimmed.Equals("🌏 global ai", StringComparison.OrdinalIgnoreCase);
        var isSaved = trimmed.Equals("/saved", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("saved", StringComparison.OrdinalIgnoreCase);
        var isTranscript = trimmed.StartsWith("/transcript", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("transcript ", StringComparison.OrdinalIgnoreCase);
        var isCheck = trimmed.StartsWith("/check", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("check ", StringComparison.OrdinalIgnoreCase)
                   || trimmed.StartsWith("/verify", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("verify ", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("🛡️ verify url", StringComparison.OrdinalIgnoreCase);
        var isRawUrl = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (isStart)
        {
            var welcome = "👋 <b>Welcome to SIGNAL Intelligence</b>\n" +
                          "<i>Your personal information firewall. Signal over noise.</i>\n\n" +
                          "🟢 <b>Status:</b> 24/7 Active Autonomous Ingestion\n" +
                          "⏱️ <b>Auto-Sync:</b> Runs every 30 minutes (US, China, Japan feeds)\n" +
                          "🛡️ <b>Video & Reel Legitimacy:</b> Scam & clickbait detector for YouTube & Instagram\n\n" +
                          "<b>Interactive Controls:</b>\n" +
                          "• <code>/today</code> — Top curated discoveries with summaries & links\n" +
                          "• <code>/offers</code> — Free AI credits, API grants (Claude, OpenAI, Gemini, Muse, Cursor)\n" +
                          "• <code>/tools</code> — Trending open-source developer tools, CLI utilities & GitHub repos\n" +
                          "• <code>/global</code> — Chinese (DeepSeek, Qwen) & Japanese (Qiita, Hatena) AI news translated to English\n" +
                          "• <code>/verify &lt;url&gt;</code> — Extract transcript & check if YouTube/Instagram video is legit or scam\n" +
                          "• <code>/transcript &lt;url&gt;</code> — Extract timestamped transcript or audio script\n" +
                          "• <code>/saved</code> — View your saved library\n\n" +
                          "💡 <i>Tip: You can paste ANY YouTube, Reel, or Article link directly into chat to inspect it!</i>";

            return new TelegramReplyResult(welcome, CreateMainNavigationKeyboard());
        }

        if (isHelp)
        {
            var help = "📖 <b>SIGNAL Bot Commands & Operation</b>\n\n" +
                       "• <code>/today</code> — High-signal curated news with summaries & links\n" +
                       "• <code>/offers</code> — Free credits, grants & tiers (Claude, OpenAI, Gemini, Muse, Cursor, etc.)\n" +
                       "• <code>/tools</code> — Open-source developer tools, CLI utilities & GitHub repos\n" +
                       "• <code>/global</code> — Chinese & Japanese platforms translated to English\n" +
                       "• <code>/verify &lt;url&gt;</code> — Verify YouTube, Reel, or Article link & check legitimacy\n" +
                       "• <code>/transcript &lt;url&gt;</code> — Pull full video/reel transcript\n" +
                       "• <code>/saved</code> — Items saved in personal library\n\n" +
                       "🔄 <b>Automatic Crawl:</b> Runs every 30 minutes, 24/7. When new high-signal items, opportunities, or tools are discovered, you receive an instant alert!\n\n" +
                       "📊 <b>Google Sheets Integration:</b>\n" +
                       "• Opportunities save to the <b>'Opportunities'</b> sheet tab.\n" +
                       "• GitHub tools save to the dedicated <b>'Tools'</b> sheet tab.\n\n" +
                       "💡 <i>Tap the menu buttons below for instant 1-tap navigation.</i>";

            return new TelegramReplyResult(help, CreateMainNavigationKeyboard());
        }

        if (isCheck && trimmed.Equals("🛡️ verify url", StringComparison.OrdinalIgnoreCase))
        {
            return "🛡️ <b>VERIFY VIDEO, REEL OR ARTICLE LINK</b>\n\n" +
                   "Paste any YouTube video link, Instagram Reel, TikTok, GitHub repo, or news URL directly into the chat.\n\n" +
                   "SIGNAL will automatically:\n" +
                   "1. 🎙️ <b>Extract full transcript & captions</b>\n" +
                   "2. 🛡️ <b>Detect scams, malware, & fake bypass claims</b>\n" +
                   "3. 🔍 <b>Verify official documentation</b>\n" +
                   "4. 💾 <b>Save to your database & Google Sheet</b>";
        }

        if (isTranscript)
        {
            var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return "⚠️ <b>Usage:</b> <code>/transcript &lt;url&gt;</code>\nExample: <code>/transcript https://www.youtube.com/watch?v=...</code>";
            }
            var targetUrl = parts[1].Trim();
            var inspection = await _videoInspector.InspectUrlAsync(targetUrl, cancellationToken);
            var transText = !string.IsNullOrWhiteSpace(inspection.Transcript)
                ? inspection.Transcript
                : inspection.Description ?? "No transcript or audio content found.";

            if (transText.Length > 3700)
            {
                transText = transText[..3690] + "\n\n<i>[...Transcript continues - truncated for Telegram length limit]</i>";
            }

            return $"🎙️ <b>VIDEO & AUDIO TRANSCRIPT</b>\n" +
                   $"<b><a href=\"{inspection.Url}\">{WebUtility.HtmlEncode(inspection.Title)}</a></b>\n" +
                   $"📍 <i>{inspection.Platform}</i> | 👤 <i>{WebUtility.HtmlEncode(inspection.Author ?? "Creator")}</i>\n\n" +
                   $"{WebUtility.HtmlEncode(transText)}";
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
                    return "⚠️ <b>Usage:</b> <code>/verify &lt;url&gt;</code>\nExample: <code>/verify https://www.youtube.com/watch?v=...</code>";
                }
                url = parts[1].Trim();
            }

            return await HandleCheckUrlAsync(url, db, cancellationToken);
        }

        if (isToday)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-36);
            var items = await db.ContentItems
                .AsNoTracking()
                .Include(c => c.Source)
                .Where(c => c.PublishedAt >= cutoff)
                .OrderByDescending(c => c.PublishedAt)
                .Take(25)
                .ToListAsync(cancellationToken);

            if (items.Count == 0)
            {
                items = await db.ContentItems
                    .AsNoTracking()
                    .Include(c => c.Source)
                    .OrderByDescending(c => c.PublishedAt)
                    .Take(25)
                    .ToListAsync(cancellationToken);
            }

            if (items.Count == 0)
            {
                return "ℹ️ <b>SIGNAL — TODAY</b>\nNo discoveries recorded yet. Background sync is running every 30 minutes.";
            }

            var aiNews = items.Where(it => IsAiItem(it)).Take(3).ToList();
            var aiItemIds = new HashSet<Guid>(aiNews.Select(x => x.Id));

            var tools = items.Where(it => !aiItemIds.Contains(it.Id) && IsSoftwareItem(it)).Take(3).ToList();
            var toolItemIds = new HashSet<Guid>(tools.Select(x => x.Id));

            var freebies = items.Where(it => !aiItemIds.Contains(it.Id) && !toolItemIds.Contains(it.Id) && IsFreebieItem(it)).Take(3).ToList();
            var freebieItemIds = new HashSet<Guid>(freebies.Select(x => x.Id));

            var ecosystem = items.Where(it => !aiItemIds.Contains(it.Id) && !toolItemIds.Contains(it.Id) && !freebieItemIds.Contains(it.Id)).Take(3).ToList();

            var banner = items.FirstOrDefault(it => !string.IsNullOrWhiteSpace(it.ImageUrl))?.ImageUrl;
            var bannerPrefix = !string.IsNullOrWhiteSpace(banner) ? $"<a href=\"{banner}\">&#8205;</a>" : string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"{bannerPrefix}🔥 <b>SIGNAL — TODAY'S CURATED INTELLIGENCE</b>");
            sb.AppendLine("<i>Segregated by topic with brief explanatory context:</i>\n");

            if (aiNews.Count > 0)
            {
                sb.AppendLine("🤖 <b>AI NEWS & OFFERS</b>");
                foreach (var it in aiNews)
                {
                    AppendDigestItem(sb, it);
                }
                sb.AppendLine();
            }

            if (tools.Count > 0)
            {
                sb.AppendLine("🛠️ <b>SOFTWARE & DEVELOPER TOOLS</b>");
                foreach (var it in tools)
                {
                    AppendDigestItem(sb, it);
                }
                sb.AppendLine();
            }

            if (freebies.Count > 0)
            {
                sb.AppendLine("🎁 <b>FREEBIES & DEALS</b>");
                foreach (var it in freebies)
                {
                    AppendDigestItem(sb, it);
                }
                sb.AppendLine();
            }

            if (ecosystem.Count > 0)
            {
                sb.AppendLine("📰 <b>TECH ECOSYSTEM & INNOVATION</b>");
                foreach (var it in ecosystem)
                {
                    AppendDigestItem(sb, it);
                }
                sb.AppendLine();
            }

            var resp = sb.ToString();
            if (resp.Length > 3900)
            {
                resp = resp[..3890] + "...";
            }
            return resp;
        }

        if (isOffers)
        {
            var opportunities = await db.Opportunities
                .AsNoTracking()
                .Include(o => o.ContentItem)
                .Where(o => o.Status != OpportunityStatus.Expired && o.Status != OpportunityStatus.Rejected)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (opportunities.Count == 0)
            {
                return "💰 <b>ACTIVE DEVELOPER OPPORTUNITIES & CREDITS</b>\nNo unreviewed opportunities in the queue right now. All caught up!\n\n<i>Auto-detection checks new feeds every 30 minutes for Claude, OpenAI, Gemini, Muse, Cursor, and open-source grants.</i>";
            }

            var banner = opportunities.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.ContentItem?.ImageUrl))?.ContentItem?.ImageUrl;
            var bannerPrefix = !string.IsNullOrWhiteSpace(banner) ? $"<a href=\"{banner}\">&#8205;</a>" : string.Empty;

            var response = $"{bannerPrefix}💰 <b>ACTIVE DEVELOPER OPPORTUNITIES & CREDITS</b>\n\n";
            for (int i = 0; i < opportunities.Count; i++)
            {
                var op = opportunities[i];
                var imgTag = !string.IsNullOrWhiteSpace(op.ContentItem?.ImageUrl) ? $" • <a href=\"{op.ContentItem.ImageUrl}\">🖼️ Preview</a>" : "";

                response += $"<b>{i + 1}. {WebUtility.HtmlEncode(op.Title)}</b>{imgTag}\n" +
                            $"   🎁 <b>Value / Grant:</b> {WebUtility.HtmlEncode(op.Value ?? "Free Tier / Credits")}\n" +
                            $"   👥 <b>Eligibility:</b> {WebUtility.HtmlEncode(op.Eligibility ?? "All developers")}\n" +
                            $"   ⏳ <b>Expires:</b> {(op.ExpiryDate.HasValue ? op.ExpiryDate.Value.ToString("dd MMM yyyy") : "Ongoing")}\n" +
                            $"   🔗 <a href=\"{op.OfficialSourceUrl ?? "https://github.com/shivadha/signal"}\">Official Link</a>\n\n";
            }
            return response;
        }

        if (isTools)
        {
            var tools = await db.ContentItems
                .AsNoTracking()
                .Include(c => c.Source)
                .Where(c => c.Url.Contains("github.com") 
                         || c.Platform == "GitHub" 
                         || (c.Category != null && (c.Category.Contains("Tool") || c.Category.Contains("Developer"))) 
                         || c.Title.ToLower().Contains("tool")
                         || c.Title.ToLower().Contains("cli")
                         || c.Title.ToLower().Contains("library")
                         || c.Title.ToLower().Contains("open-source"))
                .OrderByDescending(c => c.PublishedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (tools.Count == 0)
            {
                return "🛠️ <b>DEVELOPER TOOLS & GITHUB REPOSITORIES</b>\nNo tools recorded yet. Background monitoring is actively scanning feeds!";
            }

            var banner = tools.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.ImageUrl))?.ImageUrl;
            var bannerPrefix = !string.IsNullOrWhiteSpace(banner) ? $"<a href=\"{banner}\">&#8205;</a>" : string.Empty;

            var response = $"{bannerPrefix}🛠️ <b>NEW DEVELOPER TOOLS & GITHUB REPOSITORIES</b>\n" +
                           "<i>Trending open-source AI utilities, CLI tools & productivity accelerators:</i>\n\n";

            for (int i = 0; i < tools.Count; i++)
            {
                var t = tools[i];
                var desc = !string.IsNullOrWhiteSpace(t.Summary) ? t.Summary : t.TextContent;
                if (!string.IsNullOrWhiteSpace(desc) && desc.Length > 200)
                    desc = desc.Substring(0, 197) + "...";

                var timeAgo = FormatRelativeTime(t.PublishedAt);
                var imgTag = !string.IsNullOrWhiteSpace(t.ImageUrl) ? $" • <a href=\"{t.ImageUrl}\">🖼️ Preview</a>" : "";

                response += $"<b>{i + 1}. <a href=\"{t.Url}\">{WebUtility.HtmlEncode(t.Title)}</a></b>{imgTag}\n" +
                            $"   🏷️ <b>Category:</b> {WebUtility.HtmlEncode(t.Category ?? "Developer Tool")} | 🕒 <i>{timeAgo}</i>\n";

                if (!string.IsNullOrWhiteSpace(desc))
                {
                    response += $"   📝 <b>Details:</b> <i>{WebUtility.HtmlEncode(desc)}</i>\n";
                }
                response += "\n";
            }
            return response;
        }

        if (isGlobal)
        {
            var globalItems = await db.ContentItems
                .AsNoTracking()
                .Include(c => c.Source)
                .Where(c => c.Source != null && (c.Source.Country == "CN" || c.Source.Country == "JP" || c.Source.Language != "en" || (c.Category != null && (c.Category.Contains("Chinese") || c.Category.Contains("Japanese")))))
                .OrderByDescending(c => c.PublishedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (globalItems.Count == 0)
            {
                globalItems = await db.ContentItems
                    .AsNoTracking()
                    .Include(c => c.Source)
                    .Where(c => c.Platform == "DeepSeek" || c.Platform == "Qwen" || c.Title.Contains("DeepSeek") || c.Title.Contains("Qwen") || c.Title.Contains("China") || c.Title.Contains("Japan"))
                    .OrderByDescending(c => c.PublishedAt)
                    .Take(5)
                    .ToListAsync(cancellationToken);
            }

            if (globalItems.Count == 0)
            {
                return "🌏 <b>GLOBAL AI INTELLIGENCE (CN / JP / US)</b>\nNo international items indexed yet. Crawlers are fetching Chinese & Japanese feeds on the 30-minute interval.";
            }

            var banner = globalItems.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.ImageUrl))?.ImageUrl;
            var bannerPrefix = !string.IsNullOrWhiteSpace(banner) ? $"<a href=\"{banner}\">&#8205;</a>" : string.Empty;

            var response = $"{bannerPrefix}🌏 <b>GLOBAL AI INTELLIGENCE (CN / JP / US)</b>\n" +
                           "<i>Latest international releases, auto-translated to English:</i>\n\n";

            for (int i = 0; i < globalItems.Count; i++)
            {
                var g = globalItems[i];
                var flag = g.Source?.Country switch
                {
                    "CN" => "🇨🇳",
                    "JP" => "🇯🇵",
                    _ => "🌐"
                };

                var desc = !string.IsNullOrWhiteSpace(g.Summary) ? g.Summary : g.TextContent;
                if (!string.IsNullOrWhiteSpace(desc) && desc.Length > 200)
                    desc = desc.Substring(0, 197) + "...";

                var timeAgo = FormatRelativeTime(g.PublishedAt);
                var imgTag = !string.IsNullOrWhiteSpace(g.ImageUrl) ? $" • <a href=\"{g.ImageUrl}\">🖼️ Preview</a>" : "";

                response += $"<b>{i + 1}. {flag} <a href=\"{g.Url}\">{WebUtility.HtmlEncode(g.Title)}</a></b>{imgTag}\n" +
                            $"   🏢 <b>Source:</b> {WebUtility.HtmlEncode(g.Source?.Name ?? g.Platform)} | 🕒 <i>{timeAgo}</i>\n";

                if (!string.IsNullOrWhiteSpace(desc))
                {
                    response += $"   📝 <b>English Translation:</b> <i>{WebUtility.HtmlEncode(desc)}</i>\n";
                }
                response += "\n";
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

    private async Task<TelegramReplyResult> HandleCheckUrlAsync(string url, ISignalDbContext db, CancellationToken ct)
    {
        var canonicalUrl = _normalizer.CanonicalizeUrl(url);

        // Run deep inspection (transcript + legitimacy + claims + risks)
        var inspection = await _videoInspector.InspectUrlAsync(url, ct);

        // Check if item already exists in database
        var existing = await db.ContentItems
            .Include(c => c.Source)
            .FirstOrDefaultAsync(c => c.Url == url || c.CanonicalUrl == canonicalUrl, ct);

        ContentItem itemToTrack;
        if (existing != null)
        {
            itemToTrack = existing;
            if (string.IsNullOrWhiteSpace(itemToTrack.TextContent) && !string.IsNullOrWhiteSpace(inspection.Transcript))
            {
                itemToTrack.TextContent = inspection.Transcript;
                await db.SaveChangesAsync(ct);
            }
        }
        else
        {
            var defaultSource = await db.Sources.FirstOrDefaultAsync(s => s.Name == "Direct Link / Inspection", ct);
            if (defaultSource == null)
            {
                defaultSource = new Source
                {
                    Name = "Direct Link / Inspection",
                    Url = "https://signal.local",
                    SourceType = SourceType.Social,
                    Category = "Manual Inspection",
                    TrustTier = TrustTier.Tier5_Unknown,
                    IsActive = true
                };
                db.Sources.Add(defaultSource);
                await db.SaveChangesAsync(ct);
            }

            // Persist inspected item into SQLite database
            itemToTrack = new ContentItem
            {
                SourceId = defaultSource.Id,
                Title = inspection.Title,
                Url = url,
                CanonicalUrl = canonicalUrl,
                Author = inspection.Author,
                Platform = inspection.Platform,
                Category = inspection.IsLegit ? "Verified Video / Tool" : "Suspicious Content",
                Summary = inspection.Summary,
                TextContent = inspection.Transcript,
                ImageUrl = inspection.ImageUrl,
                DiscoveredAt = DateTimeOffset.UtcNow,
                PublishedAt = DateTimeOffset.UtcNow,
                ContentHash = _normalizer.ComputeContentHash(inspection.Title, inspection.Transcript ?? ""),
                Language = "en",
                IsDuplicate = false
            };

            db.ContentItems.Add(itemToTrack);
            await db.SaveChangesAsync(ct);
        }

        var badge = inspection.LegitimacyVerdict.Contains("LEGITIMATE") ? "🟢" : (inspection.LegitimacyVerdict.Contains("DANGEROUS") ? "🔴" : "🟡");
        var imagePrefix = !string.IsNullOrWhiteSpace(inspection.ImageUrl)
            ? $"<a href=\"{inspection.ImageUrl}\">&#8205;</a>"
            : string.Empty;

        var card = $"{imagePrefix}{badge} <b>SIGNAL VIDEO & LINK VERIFICATION</b>\n\n" +
                   $"<b><a href=\"{url}\">{WebUtility.HtmlEncode(inspection.Title)}</a></b>\n" +
                   $"📍 <b>Platform:</b> {inspection.Platform} | 👤 <b>Creator:</b> {WebUtility.HtmlEncode(inspection.Author ?? "Unknown")}\n\n" +
                   $"🛡️ <b>Legitimacy Verdict:</b> <b>{inspection.LegitimacyVerdict}</b> ({inspection.Confidence * 100:F0}% confidence)\n" +
                   $"💡 <b>Analysis:</b> {WebUtility.HtmlEncode(inspection.SafeRecommendation ?? "Analysis completed.")}\n\n";

        if (inspection.Claims.Count > 0)
        {
            card += "📋 <b>Key Claims Detected:</b>\n" + string.Join("\n", inspection.Claims.Select(c => $"• {WebUtility.HtmlEncode(c)}")) + "\n\n";
        }

        if (inspection.RiskFactors.Count > 0)
        {
            card += "⚠️ <b>Risk Factors / Red Flags:</b>\n" + string.Join("\n", inspection.RiskFactors.Select(r => $"• {WebUtility.HtmlEncode(r)}")) + "\n\n";
        }

        if (!string.IsNullOrWhiteSpace(inspection.Transcript))
        {
            var preview = inspection.Transcript.Length > 240 ? inspection.Transcript[..237] + "..." : inspection.Transcript;
            card += $"🎙️ <b>Transcript / Audio Extract:</b>\n<i>{WebUtility.HtmlEncode(preview)}</i>\n\n";
        }

        var keyboard = new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard = new List<List<TelegramInlineKeyboardButton>>
            {
                new()
                {
                    new() { Text = "📑 Read Full Transcript", CallbackData = $"transcript:{itemToTrack.Id}" },
                    new() { Text = "⭐ Save to Sheet", CallbackData = $"savetool:{itemToTrack.Id}" }
                },
                new()
                {
                    new() { Text = "🔗 Open Video / Link", Url = url }
                }
            }
        };

        return new TelegramReplyResult(card, keyboard);
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
        object? keyboard = null,
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

    public async Task<TelegramReplyResult> HandleCallbackAsync(
        string callbackData,
        ISignalDbContext db,
        IGoogleSheetsProvider sheetsProvider,
        CancellationToken cancellationToken = default)
    {
        var parts = callbackData.Split(':', 2);
        var action = parts[0].ToLowerInvariant();
        var idStr = parts.Length > 1 ? parts[1] : string.Empty;

        if (!Guid.TryParse(idStr, out var entityId))
        {
            return "⚠️ Invalid item reference.";
        }

        if (action == "transcript")
        {
            var item = await db.ContentItems.FirstOrDefaultAsync(c => c.Id == entityId, cancellationToken);
            if (item == null)
                return "⚠️ Item not found in database.";

            var transcriptText = !string.IsNullOrWhiteSpace(item.TextContent)
                ? item.TextContent
                : item.Summary ?? "No transcript text available.";

            if (transcriptText.Length > 3700)
            {
                transcriptText = transcriptText[..3690] + "\n\n<i>[...Transcript continues - truncated for Telegram length limit]</i>";
            }

            return $"🎙️ <b>FULL TRANSCRIPT & AUDIO SCRIPT</b>\n\n" +
                   $"<b><a href=\"{item.Url}\">{WebUtility.HtmlEncode(item.Title)}</a></b>\n" +
                   $"📍 <b>Platform:</b> {WebUtility.HtmlEncode(item.Platform)}\n\n" +
                   $"{WebUtility.HtmlEncode(transcriptText)}";
        }

        if (action == "savetool")
        {
            var toolItem = await db.ContentItems.FirstOrDefaultAsync(c => c.Id == entityId, cancellationToken);
            if (toolItem == null)
                return "⚠️ Tool item not found.";

            var result = await sheetsProvider.SyncToolRepoAsync(
                toolItem.Title,
                toolItem.Url,
                toolItem.Summary ?? toolItem.TextContent,
                toolItem.Category ?? "Developer Tools",
                0.9,
                cancellationToken);

            return result.Success && result.RowIndex.HasValue
                ? $"⭐ <b>SAVED TO GOOGLE SHEET (TOOLS)</b>\nAdded <b>{WebUtility.HtmlEncode(toolItem.Title)}</b> to the <b>'Tools'</b> tab at Row #{result.RowIndex}."
                : $"⭐ <b>SAVED LOCALLY</b>\nTool recorded in local offline queue: {result.ErrorMessage ?? "Pending sync"}";
        }

        var opp = await db.Opportunities
            .Include(o => o.ContentItem)
            .FirstOrDefaultAsync(o => o.Id == entityId, cancellationToken);

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

    private static void AppendDigestItem(StringBuilder sb, ContentItem it)
    {
        var briefContext = !string.IsNullOrWhiteSpace(it.Summary) ? it.Summary : it.TextContent;
        if (!string.IsNullOrWhiteSpace(briefContext) && briefContext.Length > 160)
        {
            briefContext = briefContext[..157] + "...";
        }

        var imageTag = !string.IsNullOrWhiteSpace(it.ImageUrl) ? $" • <a href=\"{it.ImageUrl}\">🖼️ Preview</a>" : "";
        sb.AppendLine($"• <b><a href=\"{it.Url}\">{WebUtility.HtmlEncode(it.Title)}</a></b>{imageTag}");
        sb.AppendLine($"  📍 <i>{WebUtility.HtmlEncode(it.Source?.Name ?? it.Platform)}</i> | 🏷️ <i>{WebUtility.HtmlEncode(it.Category ?? "General")}</i>");
        if (!string.IsNullOrWhiteSpace(briefContext))
        {
            sb.AppendLine($"  💡 <i>{WebUtility.HtmlEncode(briefContext)}</i>");
        }
    }

    private static bool IsAiItem(ContentItem it)
    {
        var text = $"{it.Title} {it.Category} {it.Summary}".ToLowerInvariant();
        return text.Contains("ai") || text.Contains("llm") || text.Contains("model") || text.Contains("openai")
            || text.Contains("anthropic") || text.Contains("deepseek") || text.Contains("qwen")
            || text.Contains("gpt") || text.Contains("gemini") || text.Contains("claude");
    }

    private static bool IsSoftwareItem(ContentItem it)
    {
        var text = $"{it.Title} {it.Category} {it.Url}".ToLowerInvariant();
        return it.Url.Contains("github.com") || it.Platform == "GitHub" || text.Contains("tool")
            || text.Contains("software") || text.Contains("cli") || text.Contains("library")
            || text.Contains("open-source") || text.Contains("framework");
    }

    private static bool IsFreebieItem(ContentItem it)
    {
        var text = $"{it.Title} {it.Category}".ToLowerInvariant();
        return text.Contains("free") || text.Contains("deal") || text.Contains("discount")
            || text.Contains("credit") || text.Contains("grant") || text.Contains("trial")
            || text.Contains("freebies");
    }
}

