using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Signal.Application.Common.Interfaces;

namespace Signal.Application.Services;

public partial class ContentNormalizationService : IContentNormalizer
{
    private static readonly HashSet<string> TrackingQueryParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
        "fbclid", "gclid", "msclkid", "mc_cid", "mc_eid",
        "ref", "source", "feature", "si", "ref_src"
    };

    public string CanonicalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return url.Trim();

        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
        {
            return $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}{uri.AbsolutePath.TrimEnd('/')}";
        }

        var queryParams = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries);

        var filteredParams = new List<string>();
        foreach (var param in queryParams)
        {
            var parts = param.Split('=', 2);
            var key = parts[0];
            if (!TrackingQueryParams.Contains(key))
            {
                filteredParams.Add(param);
            }
        }

        var cleanQuery = filteredParams.Count > 0
            ? "?" + string.Join('&', filteredParams)
            : string.Empty;

        var cleanPath = uri.AbsolutePath.TrimEnd('/');
        return $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}{cleanPath}{cleanQuery}";
    }

    public string ComputeContentHash(string title, string? content)
    {
        var normalizedTitle = (title ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedContent = (content ?? string.Empty).Trim().ToLowerInvariant();
        var rawData = $"{normalizedTitle}|{normalizedContent}";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexStringLower(bytes);
    }

    public string CleanText(string? htmlOrRawText)
    {
        if (string.IsNullOrWhiteSpace(htmlOrRawText))
            return string.Empty;

        // Strip HTML tags
        var noHtml = HtmlTagRegex().Replace(htmlOrRawText, " ");
        // Decode HTML entities (&amp;, &quot;, etc.)
        var decoded = WebUtility.HtmlDecode(noHtml);
        // Normalize whitespace
        var normalized = WhitespaceRegex().Replace(decoded, " ").Trim();
        return normalized;
    }

    [GeneratedRegex("<.*?>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
