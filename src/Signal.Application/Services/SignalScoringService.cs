using Signal.Domain.Entities;
using Signal.Domain.Enums;

namespace Signal.Application.Services;

public record ScoreBreakdown
{
    public double SourceQualityScore { get; init; }
    public double EvidenceScore { get; init; }
    public double FreshnessScore { get; init; }
    public double RelevanceScore { get; init; }
    public double OpportunityScore { get; init; }
    public double UrgencyScore { get; init; }
    public double RageBaitPenalty { get; init; }
    public double DuplicatePenalty { get; init; }
    public double FinalScore { get; init; }
}

public class SignalScoringService
{
    public ScoreBreakdown CalculateScore(
        ContentItem item,
        Source source,
        double aiRelevance,
        double rageBaitScore,
        bool isOpportunity,
        DateTimeOffset? expiryDate)
    {
        // 1. Source Quality Score based on Trust Tier
        var sourceQuality = source.TrustTier switch
        {
            TrustTier.Tier1_Official => 1.0,
            TrustTier.Tier2_Established => 0.8,
            TrustTier.Tier3_Specialist => 0.6,
            TrustTier.Tier4_Community => 0.4,
            _ => 0.2
        };

        // 2. Evidence Score (official domain or high trust)
        var evidence = source.IsOfficial ? 1.0 : (source.TrustTier <= TrustTier.Tier2_Established ? 0.8 : 0.5);

        // 3. Freshness Score (exponential decay over 7 days)
        var ageHours = Math.Max(0, (DateTimeOffset.UtcNow - item.PublishedAt).TotalHours);
        var freshness = Math.Max(0.1, Math.Exp(-0.02 * ageHours)); // decays gracefully

        // 4. Relevance Score
        var relevance = Math.Clamp(aiRelevance, 0.0, 1.0);

        // 5. Opportunity Score
        var opportunity = isOpportunity ? 0.95 : 0.1;

        // 6. Urgency Score (if expiring in < 7 days)
        var urgency = 0.0;
        if (expiryDate.HasValue)
        {
            var remainingHours = (expiryDate.Value - DateTimeOffset.UtcNow).TotalHours;
            if (remainingHours <= 0)
                urgency = 0.0; // Expired
            else if (remainingHours <= 24)
                urgency = 1.0;
            else if (remainingHours <= 72)
                urgency = 0.8;
            else if (remainingHours <= 168)
                urgency = 0.5;
        }

        // Penalties
        var rageBaitPenalty = rageBaitScore * 0.4;
        var duplicatePenalty = item.IsDuplicate ? 0.7 : 0.0;

        // Weighted Final Score
        var baseScore = (sourceQuality * 0.20)
                      + (evidence * 0.20)
                      + (relevance * 0.25)
                      + (freshness * 0.15)
                      + (opportunity * 0.15)
                      + (urgency * 0.05);

        var finalScore = Math.Clamp(baseScore - rageBaitPenalty - duplicatePenalty, 0.0, 1.0);

        return new ScoreBreakdown
        {
            SourceQualityScore = Math.Round(sourceQuality, 2),
            EvidenceScore = Math.Round(evidence, 2),
            FreshnessScore = Math.Round(freshness, 2),
            RelevanceScore = Math.Round(relevance, 2),
            OpportunityScore = Math.Round(opportunity, 2),
            UrgencyScore = Math.Round(urgency, 2),
            RageBaitPenalty = Math.Round(rageBaitPenalty, 2),
            DuplicatePenalty = Math.Round(duplicatePenalty, 2),
            FinalScore = Math.Round(finalScore, 2)
        };
    }
}
