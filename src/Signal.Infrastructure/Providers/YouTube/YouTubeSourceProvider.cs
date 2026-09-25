using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;
using Signal.Application.Common.Models;
using Signal.Domain.Entities;
using Signal.Domain.Enums;

namespace Signal.Infrastructure.Providers.YouTube;

public class YouTubeSourceProvider : ISourceProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YouTubeSourceProvider> _logger;

    public YouTubeSourceProvider(HttpClient httpClient, ILogger<YouTubeSourceProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool CanHandle(SourceType sourceType) => sourceType == SourceType.YouTubeChannel;

    public async Task<IReadOnlyList<RawContentItem>> FetchContentAsync(
        Source source,
        CancellationToken cancellationToken = default)
    {
        var feedUrl = source.FeedUrl ?? source.Url;
        if (string.IsNullOrWhiteSpace(feedUrl))
            return Array.Empty<RawContentItem>();

        var results = new List<RawContentItem>();

        try
        {
            using var response = await _httpClient.GetAsync(feedUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = XDocument.Parse(xml);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace media = "http://search.yahoo.com/mrss/";
            XNamespace yt = "http://www.youtube.com/xml/schemas/2015";

            var entries = doc.Root?.Elements(atom + "entry");
            if (entries == null)
                return results;

            foreach (var entry in entries)
            {
                var title = entry.Element(atom + "title")?.Value ?? string.Empty;
                var videoId = entry.Element(yt + "videoId")?.Value ?? string.Empty;
                var link = entry.Element(atom + "link")?.Attribute("href")?.Value
                           ?? (string.IsNullOrEmpty(videoId) ? string.Empty : $"https://www.youtube.com/watch?v={videoId}");

                var author = entry.Element(atom + "author")?.Element(atom + "name")?.Value ?? source.Name;
                var publishedStr = entry.Element(atom + "published")?.Value;
                var published = DateTimeOffset.TryParse(publishedStr, out var dto) ? dto : DateTimeOffset.UtcNow;

                var mediaGroup = entry.Element(media + "group");
                var description = mediaGroup?.Element(media + "description")?.Value;

                if (!string.IsNullOrEmpty(link) && !string.IsNullOrEmpty(title))
                {
                    results.Add(new RawContentItem
                    {
                        Title = title,
                        Url = link,
                        Author = author,
                        PublishedAt = published,
                        Summary = description,
                        TextContent = description,
                        Language = source.Language,
                        Category = "Video / AI"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch YouTube XML feed from {Url} for source {Source}", feedUrl, source.Name);
        }

        return results;
    }

    public static double CalculateWatchScore(string title, string? description, TimeSpan duration)
    {
        var text = $"{title} {description}".ToLowerInvariant();

        // Technical depth keywords
        var technicalTokens = new[] { "architecture", "tutorial", "code", "implementation", "deep dive", "hands on", "sdk", "benchmark" };
        var clickbaitTokens = new[] { "insane", "shocking", "you won't believe", "secret trick", "comment ai", "game over" };

        var techMatches = technicalTokens.Count(t => text.Contains(t));
        var baitMatches = clickbaitTokens.Count(t => text.Contains(t));

        // Prefer 5-25 min videos (high density) vs ultra-short 30s clips or 3-hour unstructured streams
        var durationScore = duration.TotalMinutes switch
        {
            >= 5 and <= 25 => 1.0,
            > 25 and <= 60 => 0.8,
            > 0 and < 5 => 0.5,
            _ => 0.7
        };

        var score = 0.5 + (techMatches * 0.15) - (baitMatches * 0.3);
        return Math.Clamp(score * durationScore, 0.0, 1.0);
    }
}
