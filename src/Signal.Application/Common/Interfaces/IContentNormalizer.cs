namespace Signal.Application.Common.Interfaces;

public interface IContentNormalizer
{
    /// <summary>
    /// Strips tracking query parameters (utm_*, ref, fbclid, etc.) and lowercases host.
    /// </summary>
    string CanonicalizeUrl(string url);

    /// <summary>
    /// Computes deterministic SHA-256 hash across normalized text and title.
    /// </summary>
    string ComputeContentHash(string title, string? content);

    /// <summary>
    /// Strips HTML tags, trims excessive whitespace, and decodes entities.
    /// </summary>
    string CleanText(string? htmlOrRawText);
}
