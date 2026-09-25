using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;

namespace Signal.Infrastructure.Providers.Translation;

public class FreeTranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FreeTranslationService> _logger;

    private readonly ConcurrentDictionary<string, string> _cache = new();

    // Detect CJK (Chinese, Japanese, Korean) + Cyrillic + Arabic + Thai + Devanagari scripts
    private static readonly Regex NonEnglishScriptsRegex = new(
        @"[\u4e00-\u9fa5\u3040-\u309f\u30a0-\u30ff\uac00-\ud7af\u1100-\u11ff\u0400-\u04ff\u0600-\u06ff\u0900-\u097f\u0e00-\u0e7f]",
        RegexOptions.Compiled);

    private static readonly Regex JapaneseRegex = new(@"[\u3040-\u309f\u30a0-\u30ff]", RegexOptions.Compiled);
    private static readonly Regex KoreanRegex = new(@"[\uac00-\ud7af\u1100-\u11ff]", RegexOptions.Compiled);
    private static readonly Regex ChineseRegex = new(@"[\u4e00-\u9fa5]", RegexOptions.Compiled);
    private static readonly Regex CyrillicRegex = new(@"[\u0400-\u04ff]", RegexOptions.Compiled);
    private static readonly Regex ArabicRegex = new(@"[\u0600-\u06ff]", RegexOptions.Compiled);

    public FreeTranslationService(HttpClient httpClient, ILogger<FreeTranslationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool NeedsTranslation(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return NonEnglishScriptsRegex.IsMatch(text);
    }

    public string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "en";
        if (JapaneseRegex.IsMatch(text)) return "ja";
        if (KoreanRegex.IsMatch(text)) return "ko";
        if (ChineseRegex.IsMatch(text)) return "zh-CN";
        if (CyrillicRegex.IsMatch(text)) return "ru";
        if (ArabicRegex.IsMatch(text)) return "ar";
        if (!NeedsTranslation(text)) return "en";
        return "auto";
    }

    public string SanitizeToEnglish(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (!NeedsTranslation(text)) return text;

        // Strip any residual non-Latin foreign characters
        var cleaned = NonEnglishScriptsRegex.Replace(text, " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? "[International Tech Update]" : cleaned;
    }

    public async Task<string> TranslateToEnglishAsync(string text, string? sourceLanguage = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // If English and doesn't contain non-Latin foreign scripts, return as-is
        if ((sourceLanguage?.Equals("en", StringComparison.OrdinalIgnoreCase) == true) && !NeedsTranslation(text))
        {
            return text;
        }

        if (_cache.TryGetValue(text, out var cached) && !NeedsTranslation(cached))
        {
            return cached;
        }

        var detected = DetectLanguage(text);
        var sl = !string.IsNullOrWhiteSpace(sourceLanguage) && !sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? sourceLanguage
            : (detected != "auto" ? detected : "auto");

        var textToTranslate = text.Length > 2000 ? text[..1997] + "..." : text;
        var encoded = Uri.EscapeDataString(textToTranslate);

        // --- Engine 1: Google Translate GTX Single API ---
        try
        {
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sl}&tl=en&dt=t&q={encoded}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var sentences = root[0];
                    if (sentences.ValueKind == JsonValueKind.Array)
                    {
                        var sb = new StringBuilder();
                        foreach (var sentence in sentences.EnumerateArray())
                        {
                            if (sentence.ValueKind == JsonValueKind.Array && sentence.GetArrayLength() > 0)
                            {
                                var part = sentence[0].GetString();
                                if (!string.IsNullOrEmpty(part))
                                {
                                    sb.Append(part);
                                }
                            }
                        }

                        var translated = sb.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            var sanitized = SanitizeToEnglish(translated);
                            _cache[text] = sanitized;
                            return sanitized;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Engine 1 (Google GTX) translation failed for '{Text}'", textToTranslate);
        }

        // --- Engine 2: Google Clients5 API (Chrome extension endpoint) ---
        try
        {
            var url = $"https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl={sl}&tl=en&q={encoded}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");

            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var first = doc.RootElement[0];
                    string? translated = null;
                    if (first.ValueKind == JsonValueKind.Array && first.GetArrayLength() > 0)
                    {
                        translated = first[0].GetString();
                    }
                    else if (first.ValueKind == JsonValueKind.String)
                    {
                        translated = first.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(translated))
                    {
                        var sanitized = SanitizeToEnglish(translated);
                        _cache[text] = sanitized;
                        return sanitized;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Engine 2 (Google Clients5) translation failed for '{Text}'", textToTranslate);
        }

        // --- Engine 3: MyMemory API with accurate language pair ---
        try
        {
            var myMemLang = sl switch
            {
                "ja" => "ja",
                "ko" => "ko",
                "zh-CN" => "zh-CN",
                "zh" => "zh-CN",
                "ru" => "ru",
                _ => detected != "auto" ? detected : "zh-CN"
            };

            var fallbackUrl = $"https://api.mymemory.translated.net/get?q={encoded}&langpair={myMemLang}|en";
            using var fbReq = new HttpRequestMessage(HttpMethod.Get, fallbackUrl);
            fbReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            using var fbResp = await _httpClient.SendAsync(fbReq, cancellationToken);
            if (fbResp.IsSuccessStatusCode)
            {
                var fbJson = await fbResp.Content.ReadAsStringAsync(cancellationToken);
                using var fbDoc = JsonDocument.Parse(fbJson);
                if (fbDoc.RootElement.TryGetProperty("responseData", out var respData) &&
                    respData.TryGetProperty("translatedText", out var transText))
                {
                    var fbResult = transText.GetString();
                    if (!string.IsNullOrWhiteSpace(fbResult) && !fbResult.StartsWith("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase))
                    {
                        var decoded = WebUtility.HtmlDecode(fbResult).Trim();
                        var sanitized = SanitizeToEnglish(decoded);
                        _cache[text] = sanitized;
                        return sanitized;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Engine 3 (MyMemory) translation failed for '{Text}'", textToTranslate);
        }

        // --- Engine 4: Lingva Public Translate API ---
        try
        {
            var lingvaLang = sl switch
            {
                "zh-CN" => "zh",
                _ => sl == "auto" ? "auto" : sl
            };

            var lingvaUrl = $"https://lingva.ml/api/v1/{lingvaLang}/en/{encoded}";
            using var lReq = new HttpRequestMessage(HttpMethod.Get, lingvaUrl);
            lReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
            using var lResp = await _httpClient.SendAsync(lReq, cancellationToken);
            if (lResp.IsSuccessStatusCode)
            {
                var lJson = await lResp.Content.ReadAsStringAsync(cancellationToken);
                using var lDoc = JsonDocument.Parse(lJson);
                if (lDoc.RootElement.TryGetProperty("translation", out var transElem))
                {
                    var lResult = transElem.GetString();
                    if (!string.IsNullOrWhiteSpace(lResult))
                    {
                        var sanitized = SanitizeToEnglish(lResult);
                        _cache[text] = sanitized;
                        return sanitized;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Engine 4 (Lingva) translation failed for '{Text}'", textToTranslate);
        }

        // --- Fallback Synthesis & Strict English Guarantee ---
        // If all remote translation APIs are unreachable or rate-limited:
        // Extract English/Latin tokens from original text (e.g. tech names like Claude, MCP, Next.js, API, Docker)
        var latinTokens = Regex.Matches(text, @"[A-Za-z0-9_.-]{2,}")
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var langLabel = sl switch
        {
            "ja" => "Japanese",
            "zh-CN" => "Chinese",
            "zh" => "Chinese",
            "ko" => "Korean",
            "ru" => "Russian",
            "ar" => "Arabic",
            _ => "International"
        };

        var keywords = string.Join(" ", latinTokens);
        var fallbackTitle = !string.IsNullOrWhiteSpace(keywords)
            ? $"[{langLabel} AI & Tech Release]: {keywords}"
            : $"[{langLabel} Tech Update] Autonomous Software & Engineering Discovery";

        _logger.LogWarning("All translation engines exhausted. Generated English fallback: {Fallback}", fallbackTitle);
        _cache[text] = fallbackTitle;
        return fallbackTitle;
    }
}
