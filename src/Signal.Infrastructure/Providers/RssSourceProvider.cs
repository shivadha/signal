using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;
using Signal.Application.Common.Models;
using Signal.Domain.Entities;
using Signal.Domain.Enums;

namespace Signal.Infrastructure.Providers;

public class RssSourceProvider : ISourceProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RssSourceProvider> _logger;

    public RssSourceProvider(HttpClient httpClient, ILogger<RssSourceProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool CanHandle(SourceType sourceType) =>
        sourceType is SourceType.Rss or SourceType.OfficialBlog or SourceType.YouTubeChannel or SourceType.Reddit;

    public async Task<IReadOnlyList<RawContentItem>> FetchContentAsync(Source source, CancellationToken cancellationToken = default)
    {
        var targetUrl = !string.IsNullOrWhiteSpace(source.FeedUrl) ? source.FeedUrl : source.Url;
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return Array.Empty<RawContentItem>();
        }

        var results = new List<RawContentItem>();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", "SignalBot/1.0 (by /u/signal_bot; +https://github.com/shivadha/signal)");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                Async = true,
                MaxCharactersInDocument = 10_000_000
            };

            using var xmlReader = XmlReader.Create(stream, settings);
            var feed = SyndicationFeed.Load(xmlReader);

            if (feed == null)
            {
                return results;
            }

            foreach (var item in feed.Items)
            {
                var link = item.Links.FirstOrDefault()?.Uri?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(link))
                    continue;

                var title = item.Title?.Text ?? string.Empty;
                var summary = item.Summary?.Text;
                var content = (item.Content as TextSyndicationContent)?.Text;

                var publishedDate = item.PublishDate != DateTimeOffset.MinValue
                    ? item.PublishDate
                    : (item.LastUpdatedTime != DateTimeOffset.MinValue ? item.LastUpdatedTime : DateTimeOffset.UtcNow);

                var author = item.Authors.FirstOrDefault()?.Name
                             ?? item.Authors.FirstOrDefault()?.Email;

                string? imageUrl = item.Links.FirstOrDefault(l => l.RelationshipType == "enclosure" && (l.MediaType?.StartsWith("image/") == true || l.Uri?.ToString().EndsWith(".jpg") == true || l.Uri?.ToString().EndsWith(".png") == true))?.Uri?.ToString();

                if (string.IsNullOrEmpty(imageUrl))
                {
                    try
                    {
                        foreach (var ext in item.ElementExtensions)
                        {
                            if (ext.OuterName is "thumbnail" or "content")
                            {
                                var elem = ext.GetObject<System.Xml.Linq.XElement>();
                                var attr = elem.Attribute("url")?.Value;
                                if (!string.IsNullOrEmpty(attr))
                                {
                                    imageUrl = attr;
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore XML extension extraction failures
                    }
                }

                if (string.IsNullOrEmpty(imageUrl) && !string.IsNullOrEmpty(summary ?? content))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(summary ?? content ?? "", @"<img\s+[^>]*?src=[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        imageUrl = match.Groups[1].Value;
                    }
                }

                results.Add(new RawContentItem
                {
                    Title = title,
                    Url = link,
                    Author = author,
                    PublishedAt = publishedDate,
                    Summary = summary,
                    TextContent = content ?? summary,
                    Language = source.Language,
                    Category = source.Category,
                    ImageUrl = imageUrl
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch or parse RSS feed from {TargetUrl} for source {SourceName}", targetUrl, source.Name);
        }

        return results;
    }
}
