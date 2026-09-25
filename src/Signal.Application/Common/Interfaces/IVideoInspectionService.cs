namespace Signal.Application.Common.Interfaces;

public record VideoInspectionResult
{
    public string Url { get; init; } = string.Empty;
    public string Platform { get; init; } = "Web";
    public string Title { get; init; } = string.Empty;
    public string? Author { get; init; }
    public string? Description { get; init; }
    public string? Transcript { get; init; }
    public string? Summary { get; init; }
    public bool IsLegit { get; init; }
    public string LegitimacyVerdict { get; init; } = "UNVERIFIED";
    public double Confidence { get; init; }
    public List<string> Claims { get; init; } = new();
    public List<string> RiskFactors { get; init; } = new();
    public string? SafeRecommendation { get; init; }
    public string? OfficialAlternativeUrl { get; init; }
    public string? ImageUrl { get; init; }
}

public interface IVideoInspectionService
{
    Task<VideoInspectionResult> InspectUrlAsync(string url, CancellationToken cancellationToken = default);
}
