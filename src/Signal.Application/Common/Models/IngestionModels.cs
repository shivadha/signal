using Signal.Domain.Entities;

namespace Signal.Application.Common.Models;

public record RawContentItem
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? Author { get; init; }
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? TextContent { get; init; }
    public string? Summary { get; init; }
    public string Language { get; init; } = "en";
    public string? Category { get; init; }
    public string? ImageUrl { get; init; }
}

public record DuplicateCheckResult
{
    public bool IsDuplicate { get; init; }
    public string Reason { get; init; } = string.Empty;
    public Guid? ExistingContentItemId { get; init; }
}

public record IngestionSummary
{
    public int SourcesChecked { get; set; }
    public int ItemsDiscovered { get; set; }
    public int NewItemsSaved { get; set; }
    public int DuplicatesFiltered { get; set; }
    public List<ContentItem> NewItems { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public record GoogleSheetSyncResult(bool Success, int? RowIndex, string? ErrorMessage);

