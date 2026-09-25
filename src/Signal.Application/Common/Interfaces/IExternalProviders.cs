using Signal.Application.Common.Models;
using Signal.Domain.Entities;

namespace Signal.Application.Common.Interfaces;

public record AIAnalysisResult
{
    public bool IsRelevant { get; init; }
    public double RelevanceScore { get; init; }
    public double RageBaitScore { get; init; }
    public bool IsOpportunity { get; init; }
    public string? OpportunityType { get; init; }
    public string? ExtractedValue { get; init; }
    public string? Summary { get; init; }
    public bool IsTool { get; init; }
    public string? GitHubRepoUrl { get; init; }
    public List<string> Claims { get; init; } = new();
}

public interface IAIProvider
{
    string ProviderName { get; }
    Task<AIAnalysisResult> AnalyzeAsync(string title, string? text, CancellationToken cancellationToken = default);
}

public interface ITelegramProvider
{
    Task<bool> SendAlertAsync(string message, CancellationToken cancellationToken = default);
    Task<bool> SendOpportunityCardAsync(Opportunity opportunity, CancellationToken cancellationToken = default);
}

public interface IGoogleSheetsProvider
{
    Task<GoogleSheetSyncResult> SyncOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default);
    Task<GoogleSheetSyncResult> SyncToolRepoAsync(string repoTitle, string repoUrl, string? description, string? category, double relevanceScore, CancellationToken cancellationToken = default);
}

