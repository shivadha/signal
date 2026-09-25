using Signal.Application.Common.Models;
using Signal.Domain.Entities;
using Signal.Domain.Enums;

namespace Signal.Application.Common.Interfaces;

public interface ISourceProvider
{
    bool CanHandle(SourceType sourceType);
    Task<IReadOnlyList<RawContentItem>> FetchContentAsync(Source source, CancellationToken cancellationToken = default);
}
