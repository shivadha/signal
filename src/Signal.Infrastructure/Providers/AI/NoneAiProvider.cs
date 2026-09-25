using System.Text.RegularExpressions;
using Signal.Application.Common.Interfaces;

namespace Signal.Infrastructure.Providers.AI;

public partial class NoneAiProvider : IAIProvider
{
    public string ProviderName => "None (Zero-Cost Deterministic Rules)";

    // High signal keywords
    private static readonly string[] RelevantKeywords =
    {
        "ai", "llm", "agent", "coding", "claude", "openai", "gpt", "gemini",
        "deepmind", ".net", "c#", "azure", "github", "copilot", "mcp",
        "open source", "weights", "model", "benchmark", "anthropic", "hugging face"
    };

    // Opportunity keywords
    private static readonly string[] OpportunityKeywords =
    {
        "free", "credits", "free tier", "open-source", "release", "developer access",
        "free trial", "discount", "grant", "preview", "gratis"
    };

    // Clickbait & Ragebait indicators
    private static readonly string[] RageBaitPatterns =
    {
        "you won't believe", "shocking", "breaking!!!", "they don't want you to know",
        "this changes everything", "99% of people", "comment ai", "comment yes",
        "dm me", "follow me", "link in bio", "share before deleted", "secret trick",
        "insane", "mind blowing"
    };

    public Task<AIAnalysisResult> AnalyzeAsync(string title, string? text, CancellationToken cancellationToken = default)
    {
        var combined = $"{title} {text}".ToLowerInvariant();

        // 1. Relevance Score Calculation
        var matchedKeywords = RelevantKeywords.Count(k => combined.Contains(k));
        var relevanceScore = Math.Min(1.0, 0.4 + (matchedKeywords * 0.15));

        // 2. Ragebait Score Calculation
        var matchedRageBait = RageBaitPatterns.Count(p => combined.Contains(p));
        var rageBaitScore = Math.Min(1.0, matchedRageBait * 0.35);

        // 3. Opportunity Detection
        var isOpportunity = OpportunityKeywords.Any(k => combined.Contains(k));
        string? opportunityType = null;
        if (isOpportunity)
        {
            if (combined.Contains("credit") || combined.Contains("api"))
                opportunityType = "FreeApiCredits";
            else if (combined.Contains("open-source") || combined.Contains("weights"))
                opportunityType = "OpenSourceRelease";
            else if (combined.Contains("free tier"))
                opportunityType = "NewFreeTier";
            else
                opportunityType = "FreeTool";
        }

        return Task.FromResult(new AIAnalysisResult
        {
            IsRelevant = relevanceScore >= 0.55,
            RelevanceScore = Math.Round(relevanceScore, 2),
            RageBaitScore = Math.Round(rageBaitScore, 2),
            IsOpportunity = isOpportunity,
            OpportunityType = opportunityType,
            Summary = title,
            Claims = new List<string> { title }
        });
    }
}
