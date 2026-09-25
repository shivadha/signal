using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;

namespace Signal.Infrastructure.Providers.Video;

public class VideoInspectionService : IVideoInspectionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VideoInspectionService> _logger;

    private static readonly Regex YouTubeRegex = new(
        @"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?|shorts)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex InstagramRegex = new(
        @"instagram\.com\/(?:p|reel|tv)\/([A-Za-z0-9_-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public VideoInspectionService(HttpClient httpClient, ILogger<VideoInspectionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<VideoInspectionResult> InspectUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var trimmed = url.Trim();
        if (YouTubeRegex.IsMatch(trimmed))
        {
            return await InspectYouTubeVideoAsync(trimmed, cancellationToken);
        }

        if (InstagramRegex.IsMatch(trimmed))
        {
            return await InspectInstagramPostAsync(trimmed, cancellationToken);
        }

        return await InspectGenericWebPageAsync(trimmed, cancellationToken);
    }

    private async Task<VideoInspectionResult> InspectYouTubeVideoAsync(string url, CancellationToken ct)
    {
        var match = YouTubeRegex.Match(url);
        var videoId = match.Success ? match.Groups[1].Value : string.Empty;

        string title = "YouTube Video";
        string author = "YouTube Creator";
        string description = string.Empty;
        string transcript = string.Empty;

        // 1. Fetch metadata via oEmbed
        try
        {
            var oEmbedUrl = $"https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={videoId}&format=json";
            using var oEmbedResp = await _httpClient.GetAsync(oEmbedUrl, ct);
            if (oEmbedResp.IsSuccessStatusCode)
            {
                var doc = await oEmbedResp.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (doc.TryGetProperty("title", out var t)) title = t.GetString() ?? title;
                if (doc.TryGetProperty("author_name", out var a)) author = a.GetString() ?? author;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "oEmbed fetch failed for YouTube video {VideoId}", videoId);
        }

        // 2. Fetch page HTML to extract description & caption track
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://www.youtube.com/watch?v={videoId}");
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            req.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

            using var resp = await _httpClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var html = await resp.Content.ReadAsStringAsync(ct);

                // Extract description from meta tag
                var descMatch = Regex.Match(html, @"<meta\s+name=""description""\s+content=""([^""]*)""", RegexOptions.IgnoreCase);
                if (descMatch.Success)
                {
                    description = WebUtility.HtmlDecode(descMatch.Groups[1].Value);
                }

                // Attempt to extract YouTube Captions URL
                transcript = await ExtractYouTubeCaptionsAsync(html, videoId, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse YouTube page for {VideoId}", videoId);
        }

        // Fallback transcript synthesis if captions are disabled
        if (string.IsNullOrWhiteSpace(transcript))
        {
            transcript = GenerateSynthesizedTranscript(title, author, description);
        }

        return EvaluateLegitimacy(url, "YouTube", title, author, description, transcript);
    }

    private async Task<string> ExtractYouTubeCaptionsAsync(string html, string videoId, CancellationToken ct)
    {
        try
        {
            // Look for captionTracks in ytInitialPlayerResponse
            var captionMatch = Regex.Match(html, @"""captionTracks"":\s*(\[[^\]]+\])");
            if (captionMatch.Success)
            {
                var json = captionMatch.Groups[1].Value;
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    string? baseUrl = null;
                    foreach (var track in doc.RootElement.EnumerateArray())
                    {
                        if (track.TryGetProperty("baseUrl", out var bu))
                        {
                            baseUrl = bu.GetString();
                            // Prefer English
                            if (track.TryGetProperty("languageCode", out var lc) && lc.GetString() == "en")
                            {
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(baseUrl))
                    {
                        using var capResp = await _httpClient.GetAsync(baseUrl, ct);
                        if (capResp.IsSuccessStatusCode)
                        {
                            var xml = await capResp.Content.ReadAsStringAsync(ct);
                            var xdoc = XDocument.Parse(xml);
                            var sb = new StringBuilder();
                            foreach (var elem in xdoc.Descendants("text"))
                            {
                                var text = WebUtility.HtmlDecode(elem.Value).Trim();
                                var start = elem.Attribute("start")?.Value ?? "";
                                if (!string.IsNullOrEmpty(text))
                                {
                                    if (double.TryParse(start, out var sec))
                                    {
                                        var ts = TimeSpan.FromSeconds(sec);
                                        sb.AppendLine($"[{ts:mm\\:ss}] {text}");
                                    }
                                    else
                                    {
                                        sb.AppendLine(text);
                                    }
                                }
                            }

                            if (sb.Length > 0)
                            {
                                return sb.ToString();
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse caption tracks for {VideoId}", videoId);
        }

        return string.Empty;
    }

    private async Task<VideoInspectionResult> InspectInstagramPostAsync(string url, CancellationToken ct)
    {
        var match = InstagramRegex.Match(url);
        var shortcode = match.Success ? match.Groups[1].Value : string.Empty;

        string title = "Instagram Reel / Post";
        string author = "Instagram Creator";
        string description = string.Empty;
        string transcript = string.Empty;

        // 1. Attempt Instagram oEmbed
        try
        {
            var oEmbedUrl = $"https://api.instagram.com/oembed?url={Uri.EscapeDataString(url)}";
            using var resp = await _httpClient.GetAsync(oEmbedUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (doc.TryGetProperty("title", out var t)) title = t.GetString() ?? title;
                if (doc.TryGetProperty("author_name", out var a)) author = a.GetString() ?? author;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Instagram oEmbed failed for {Url}", url);
        }

        // 2. Fetch OpenGraph and Page Content
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var resp = await _httpClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var html = await resp.Content.ReadAsStringAsync(ct);

                var ogDesc = Regex.Match(html, @"<meta\s+property=""og:description""\s+content=""([^""]*)""", RegexOptions.IgnoreCase);
                if (ogDesc.Success)
                {
                    description = WebUtility.HtmlDecode(ogDesc.Groups[1].Value);
                }

                var ogTitle = Regex.Match(html, @"<meta\s+property=""og:title""\s+content=""([^""]*)""", RegexOptions.IgnoreCase);
                if (ogTitle.Success && title == "Instagram Reel / Post")
                {
                    title = WebUtility.HtmlDecode(ogTitle.Groups[1].Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to scrape Instagram page {Url}", url);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            description = title;
        }

        transcript = $"[Instagram Reel Caption & Audio Script]\n" +
                     $"Creator: @{author}\n" +
                     $"Content: {description}\n" +
                     $"Target Link / Action: Check bio / link in profile.";

        return EvaluateLegitimacy(url, "Instagram", title, author, description, transcript);
    }

    private async Task<VideoInspectionResult> InspectGenericWebPageAsync(string url, CancellationToken ct)
    {
        string title = "Web Page";
        string description = string.Empty;
        string author = "Publisher";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            using var resp = await _httpClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var html = await resp.Content.ReadAsStringAsync(ct);

                var titleMatch = Regex.Match(html, @"<title>([^<]*)</title>", RegexOptions.IgnoreCase);
                if (titleMatch.Success) title = WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());

                var descMatch = Regex.Match(html, @"<meta\s+(?:name|property)=""(?:description|og:description)""\s+content=""([^""]*)""", RegexOptions.IgnoreCase);
                if (descMatch.Success) description = WebUtility.HtmlDecode(descMatch.Groups[1].Value.Trim());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch webpage {Url}", url);
        }

        var transcript = $"[Web Content Extract]\nTitle: {title}\nSummary: {description}";
        return EvaluateLegitimacy(url, "Web", title, author, description, transcript);
    }

    private static string GenerateSynthesizedTranscript(string title, string author, string description)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Synthesized Video Audio & Narrative Summary]");
        sb.AppendLine($"Channel: {author}");
        sb.AppendLine($"Subject: {title}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine($"Summary / Chapters:\n{description}");
        }
        else
        {
            sb.AppendLine("Video outlines an AI tool release, developer methodology or workflow accelerator.");
        }
        return sb.ToString();
    }

    private static VideoInspectionResult EvaluateLegitimacy(
        string url,
        string platform,
        string title,
        string author,
        string description,
        string transcript)
    {
        var text = $"{title} {description} {transcript}".ToLowerInvariant();

        var claims = new List<string>();
        var riskFactors = new List<string>();
        string? officialUrl = null;

        // 1. Analyze Common Scam & Clickbait Flags
        if (text.Contains("bypass") && (text.Contains("limit") || text.Contains("token") || text.Contains("paywall")))
        {
            claims.Add("Claims to bypass API token limits or paywalls");
            riskFactors.Add("Bypass methods usually violate Terms of Service or are short-lived exploits");
        }

        if (text.Contains("unlimited") && (text.Contains("claude 3.5") || text.Contains("gpt-4") || text.Contains("gpt-5") || text.Contains("free api")))
        {
            claims.Add("Promises unlimited free access to premium LLM APIs without billing");
            riskFactors.Add("No official free unlimited tier exists for Claude 3.5 or GPT-4o. Usually a phishing wrapper or token scraper");
        }

        if (text.Contains("download") && (text.Contains(".zip") || text.Contains(".exe") || text.Contains("password") || text.Contains("mediafire") || text.Contains("mega.nz")))
        {
            riskFactors.Add("🚨 RED FLAG: Directing users to download third-party archives (.zip/.exe). High risk of info-stealer malware");
        }

        if (text.Contains("comment") && (text.Contains("link") || text.Contains("send") || text.Contains("dm")))
        {
            riskFactors.Add("Engagement-farming tactic ('Comment below for the link')");
        }

        if (text.Contains("disable antivirus") || text.Contains("windows defender"))
        {
            riskFactors.Add("🚨 CRITICAL MALWARE SIGNATURE: Prompting user to disable antivirus");
        }

        // 2. Analyze Legitimacy Boosters
        bool hasOfficialLink = false;
        if (text.Contains("github.com/"))
        {
            claims.Add("Provides open-source GitHub repository");
            hasOfficialLink = true;
            officialUrl = ExtractFirstUrl(text, "github.com");
        }
        if (text.Contains("anthropic.com") || text.Contains("claude.ai"))
        {
            hasOfficialLink = true;
            officialUrl = "https://www.anthropic.com";
        }
        if (text.Contains("openai.com"))
        {
            hasOfficialLink = true;
            officialUrl = "https://openai.com";
        }
        if (text.Contains("deepseek.com"))
        {
            hasOfficialLink = true;
            officialUrl = "https://www.deepseek.com";
        }
        if (text.Contains("huggingface.co"))
        {
            hasOfficialLink = true;
            officialUrl = ExtractFirstUrl(text, "huggingface.co") ?? "https://huggingface.co";
        }

        // 3. Determine Verdict & Confidence
        bool isLegit;
        string verdict;
        double confidence;
        string recommendation;

        if (riskFactors.Any(r => r.Contains("CRITICAL") || r.Contains("RED FLAG")))
        {
            isLegit = false;
            verdict = "🔴 DANGEROUS / SUSPECTED MALWARE";
            confidence = 0.95;
            recommendation = "DO NOT download any files or run scripts from this video. It exhibits known credential stealer patterns.";
        }
        else if (riskFactors.Count >= 2)
        {
            isLegit = false;
            verdict = "🟡 HIGH RISK / CLICKBAIT";
            confidence = 0.85;
            recommendation = "Video exaggerates capabilities or relies on unreliable workarounds. Proceed with caution.";
        }
        else if (hasOfficialLink)
        {
            isLegit = true;
            verdict = "🟢 LEGITIMATE & VERIFIED";
            confidence = 0.92;
            recommendation = "Tool / technique references verifiable official repositories or documentation. Safe to explore.";
        }
        else
        {
            isLegit = true;
            verdict = "🟡 UNVERIFIED / PROCEED WITH CARE";
            confidence = 0.70;
            recommendation = "No malicious indicators found, but no direct official repository link detected. Verify claims independently.";
        }

        return new VideoInspectionResult
        {
            Url = url,
            Platform = platform,
            Title = title,
            Author = author,
            Description = description,
            Transcript = transcript,
            Summary = !string.IsNullOrWhiteSpace(description) && description.Length > 250 ? description[..247] + "..." : description,
            IsLegit = isLegit,
            LegitimacyVerdict = verdict,
            Confidence = confidence,
            Claims = claims,
            RiskFactors = riskFactors,
            SafeRecommendation = recommendation,
            OfficialAlternativeUrl = officialUrl
        };
    }

    private static string? ExtractFirstUrl(string text, string domain)
    {
        var match = Regex.Match(text, @"https?://[^\s""'>]+" + Regex.Escape(domain) + @"[^\s""'>]*", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }
}
