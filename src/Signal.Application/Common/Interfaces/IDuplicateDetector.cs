using Signal.Application.Common.Models;

namespace Signal.Application.Common.Interfaces;

public interface IDuplicateDetector
{
    Task<DuplicateCheckResult> CheckDuplicateAsync(
        string url,
        string canonicalUrl,
        string contentHash,
        CancellationToken cancellationToken = default);
}
