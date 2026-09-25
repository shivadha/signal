using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;
using Signal.Application.Common.Models;
using Signal.Domain.Entities;

namespace Signal.Application.Services;

public class SourceIngestionService
{
    private readonly ISignalDbContext _dbContext;
    private readonly IEnumerable<ISourceProvider> _sourceProviders;
    private readonly IContentNormalizer _normalizer;
    private readonly IDuplicateDetector _duplicateDetector;
    private readonly ILogger<SourceIngestionService> _logger;

    public SourceIngestionService(
        ISignalDbContext dbContext,
        IEnumerable<ISourceProvider> sourceProviders,
        IContentNormalizer normalizer,
        IDuplicateDetector duplicateDetector,
        ILogger<SourceIngestionService> logger)
    {
        _dbContext = dbContext;
        _sourceProviders = sourceProviders;
        _normalizer = normalizer;
        _duplicateDetector = duplicateDetector;
        _logger = logger;
    }

    public async Task<IngestionSummary> IngestAllActiveSourcesAsync(CancellationToken cancellationToken = default)
    {
        var summary = new IngestionSummary();

        var activeSources = await _dbContext.Sources
            .Where(s => s.IsActive)
            .ToListAsync(cancellationToken);

        summary.SourcesChecked = activeSources.Count;

        foreach (var source in activeSources)
        {
            try
            {
                var provider = _sourceProviders.FirstOrDefault(p => p.CanHandle(source.SourceType));
                if (provider == null)
                {
                    _logger.LogWarning("No registered provider for SourceType {SourceType} (Source: {SourceName})", source.SourceType, source.Name);
                    continue;
                }

                _logger.LogInformation("Ingesting from source: {SourceName} ({FeedUrl})", source.Name, source.FeedUrl);
                var rawItems = await provider.FetchContentAsync(source, cancellationToken);
                summary.ItemsDiscovered += rawItems.Count;

                foreach (var raw in rawItems)
                {
                    var cleanTitle = _normalizer.CleanText(raw.Title);
                    var cleanText = _normalizer.CleanText(raw.TextContent ?? raw.Summary);
                    var canonicalUrl = _normalizer.CanonicalizeUrl(raw.Url);
                    var contentHash = _normalizer.ComputeContentHash(cleanTitle, cleanText);

                    var duplicateResult = await _duplicateDetector.CheckDuplicateAsync(
                        raw.Url, canonicalUrl, contentHash, cancellationToken);

                    if (duplicateResult.IsDuplicate)
                    {
                        summary.DuplicatesFiltered++;
                        continue;
                    }

                    var contentItem = new ContentItem
                    {
                        SourceId = source.Id,
                        Platform = source.SourceType.ToString(),
                        Title = cleanTitle,
                        Url = raw.Url,
                        CanonicalUrl = canonicalUrl,
                        Author = raw.Author,
                        PublishedAt = raw.PublishedAt,
                        DiscoveredAt = DateTimeOffset.UtcNow,
                        ContentHash = contentHash,
                        TextContent = cleanText,
                        Language = raw.Language,
                        Category = raw.Category ?? source.Category,
                        Summary = _normalizer.CleanText(raw.Summary),
                        IsDuplicate = false
                    };

                    _dbContext.ContentItems.Add(contentItem);
                    summary.NewItemsSaved++;
                }

                source.LastFetchedAt = DateTimeOffset.UtcNow;
                source.UpdatedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest source {SourceName}", source.Name);
                summary.Errors.Add($"Source {source.Name}: {ex.Message}");
            }
        }

        if (summary.NewItemsSaved > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return summary;
    }
}
