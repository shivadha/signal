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
        "open source", "weights", "model", "benchmark", "anthropic", "hugging face",
        "mistral", "perplexity", "cursor", "groq", "together", "replicate", "fireworks",
        "elevenlabs", "deepgram", "cohere", "suno", "udio", "midjourney", "muse", "vllm", "ollama"
    };

    // Opportunity & Credit keywords
    private static readonly string[] OpportunityKeywords =
    {
        "free", "credits", "credit", "free tier", "open-source", "release", "developer access",
        "free trial", "discount", "grant", "preview", "gratis", "free tokens", "token grant",
        "free compute", "voucher", "coupon", "promo", "giveaway", "free pro", "free access"
    };

    // Productivity & Tool keywords
    private static readonly string[] ToolKeywords =
    {
        "tool", "cli", "library", "utility", "productivity", "automation", "workflow",
        "wrapper", "sdk", "extension", "plugin", "proxy", "workaround", "scraper", "github.com"
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
        var rawText = $"{title} {text}";
        var combined = rawText.ToLowerInvariant();

        // 1. Relevance Score Calculation
        var matchedKeywords = RelevantKeywords.Count(k => combined.Contains(k));
        var relevanceScore = Math.Min(1.0, 0.4 + (matchedKeywords * 0.15));

        // 2. Ragebait Score Calculation
        var matchedRageBait = RageBaitPatterns.Count(p => combined.Contains(p));
        var rageBaitScore = Math.Min(1.0, matchedRageBait * 0.35);

        // 3. Opportunity Detection (Free credits, grants, deals)
        var isOpportunity = OpportunityKeywords.Any(k => combined.Contains(k));
        string? opportunityType = null;
        string? extractedValue = null;

        if (isOpportunity)
        {
            if (combined.Contains("credit") || combined.Contains("api grant") || combined.Contains("token grant"))
                opportunityType = "FreeApiCredits";
            else if (combined.Contains("cloud credit") || combined.Contains("compute"))
                opportunityType = "FreeCloudCredits";
            else if (combined.Contains("free trial") || combined.Contains("pro free"))
                opportunityType = "FreeTrial";
            else if (combined.Contains("open-source") || combined.Contains("weights") || combined.Contains("release"))
                opportunityType = "OpenSourceRelease";
            else if (combined.Contains("free tier") || combined.Contains("free plan"))
                opportunityType = "NewFreeTier";
            else
                opportunityType = "FreeTool";

            // Extract monetary value if present (e.g. $100, $5, $1000)
            var valMatch = Regex.Match(rawText, @"\$\d+(?:,\d{3})*(?:\.\d{2})?");
            if (valMatch.Success)
            {
                extractedValue = valMatch.Value;
            }
        }

        // 4. Tool & GitHub Repo Detection
        var gitHubMatch = Regex.Match(rawText, @"https?://(?:www\.)?github\.com/[a-zA-Z0-9_\.-]+/[a-zA-Z0-9_\.-]+");
        var gitHubRepoUrl = gitHubMatch.Success ? gitHubMatch.Value : null;
        var isTool = gitHubMatch.Success || ToolKeywords.Any(k => combined.Contains(k));

        return Task.FromResult(new AIAnalysisResult
        {
            IsRelevant = relevanceScore >= 0.55 || isTool,
            RelevanceScore = Math.Round(relevanceScore, 2),
            RageBaitScore = Math.Round(rageBaitScore, 2),
            IsOpportunity = isOpportunity,
            OpportunityType = opportunityType,
            ExtractedValue = extractedValue,
            Summary = title,
            IsTool = isTool,
            GitHubRepoUrl = gitHubRepoUrl,
            Claims = new List<string> { title }
        });
    }
}

