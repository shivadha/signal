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

    // Detect CJK (Chinese, Japanese, Korean) characters
    private static readonly Regex CjkRegex = new(@"[\u4e00-\u9fa5\u3040-\u309f\u30a0-\u30ff\uac00-\ud7af]", RegexOptions.Compiled);

    public FreeTranslationService(HttpClient httpClient, ILogger<FreeTranslationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool NeedsTranslation(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return CjkRegex.IsMatch(text);
    }

    public async Task<string> TranslateToEnglishAsync(string text, string? sourceLanguage = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // If English and doesn't contain non-Latin CJK scripts, skip translation
        if ((sourceLanguage?.Equals("en", StringComparison.OrdinalIgnoreCase) == true) && !NeedsTranslation(text))
        {
            return text;
        }

        try
        {
            // Truncate to safe length for query string if extremely long
            var textToTranslate = text.Length > 2000 ? text[..1997] + "..." : text;
            var encoded = Uri.EscapeDataString(textToTranslate);
            var sl = string.IsNullOrWhiteSpace(sourceLanguage) ? "auto" : sourceLanguage;
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sl}&tl=en&dt=t&q={encoded}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Translation API returned status {Status}. Returning original text.", response.StatusCode);
                return text;
            }

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

                    var translated = sb.ToString();
                    if (!string.IsNullOrWhiteSpace(translated))
                    {
                        return translated;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to translate text. Falling back to original content.");
        }

        return text;
    }
}
