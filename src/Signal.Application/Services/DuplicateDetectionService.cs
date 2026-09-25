using Microsoft.EntityFrameworkCore;
using Signal.Application.Common.Interfaces;
using Signal.Application.Common.Models;

namespace Signal.Application.Services;

public class DuplicateDetectionService : IDuplicateDetector
{
    private readonly ISignalDbContext _dbContext;

    public DuplicateDetectionService(ISignalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DuplicateCheckResult> CheckDuplicateAsync(
        string url,
        string canonicalUrl,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        // 1. Exact URL Match
        var exactMatch = await _dbContext.ContentItems
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Url == url, cancellationToken);

        if (exactMatch != null)
        {
            return new DuplicateCheckResult
            {
                IsDuplicate = true,
                Reason = "Exact URL match",
                ExistingContentItemId = exactMatch.Id
            };
        }

        // 2. Canonical URL Match
        var canonicalMatch = await _dbContext.ContentItems
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CanonicalUrl == canonicalUrl, cancellationToken);

        if (canonicalMatch != null)
        {
            return new DuplicateCheckResult
            {
                IsDuplicate = true,
                Reason = "Canonical URL match (tracking parameters removed)",
                ExistingContentItemId = canonicalMatch.Id
            };
        }

        // 3. Content SHA-256 Hash Match
        if (!string.IsNullOrEmpty(contentHash))
        {
            var hashMatch = await _dbContext.ContentItems
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ContentHash == contentHash, cancellationToken);

            if (hashMatch != null)
            {
                return new DuplicateCheckResult
                {
                    IsDuplicate = true,
                    Reason = "Content SHA-256 hash collision",
                    ExistingContentItemId = hashMatch.Id
                };
            }
        }

        return new DuplicateCheckResult
        {
            IsDuplicate = false,
            Reason = "No duplicate detected"
        };
    }
}
