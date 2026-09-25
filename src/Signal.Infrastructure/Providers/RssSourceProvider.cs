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

            SyndicationFeed? feed = null;
            try
            {
                using var xmlReader = XmlReader.Create(stream, settings);
                feed = SyndicationFeed.Load(xmlReader);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SyndicationFeed.Load failed for {TargetUrl}, attempting XDocument fallback parser.", targetUrl);
            }

            if (feed != null)
            {
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
            else
            {
                // Fallback XML parsing for RDF (RSS 1.0) and non-standard Atom
                stream.Position = 0;
                var xdoc = System.Xml.Linq.XDocument.Load(stream);
                var items = xdoc.Descendants().Where(e => e.Name.LocalName is "item" or "entry");
                foreach (var el in items)
                {
                    var link = el.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value
                               ?? el.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Value;
                    if (string.IsNullOrWhiteSpace(link)) continue;

                    var title = el.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value ?? string.Empty;
                    var summary = el.Elements().FirstOrDefault(e => e.Name.LocalName is "description" or "summary" or "content")?.Value;
                    var dateStr = el.Elements().FirstOrDefault(e => e.Name.LocalName is "pubDate" or "date" or "published" or "updated")?.Value;
                    var published = DateTimeOffset.TryParse(dateStr, out var d) ? d : DateTimeOffset.UtcNow;

                    var imgMatch = System.Text.RegularExpressions.Regex.Match(summary ?? "", @"<img\s+[^>]*?src=[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var img = imgMatch.Success ? imgMatch.Groups[1].Value : null;

                    results.Add(new RawContentItem
                    {
                        Title = title,
                        Url = link,
                        PublishedAt = published,
                        Summary = summary,
                        TextContent = summary,
                        Language = source.Language,
                        Category = source.Category,
                        ImageUrl = img
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch or parse RSS feed from {TargetUrl} for source {SourceName}", targetUrl, source.Name);
        }

        return results;
    }
}
