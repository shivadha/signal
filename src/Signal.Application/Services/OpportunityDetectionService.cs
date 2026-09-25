using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;
using Signal.Domain.Entities;
using Signal.Domain.Enums;

namespace Signal.Application.Services;

public partial class OpportunityDetectionService
{
    private readonly ISignalDbContext _dbContext;
    private readonly IAIProvider _aiProvider;
    private readonly ILogger<OpportunityDetectionService> _logger;

    public OpportunityDetectionService(
        ISignalDbContext dbContext,
        IAIProvider aiProvider,
        ILogger<OpportunityDetectionService> logger)
    {
        _dbContext = dbContext;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task<Opportunity?> EvaluateAndCreateOpportunityAsync(
        ContentItem item,
        CancellationToken cancellationToken = default)
    {
        // 1. Analyze via AI Provider (or zero-cost heuristic)
        var analysis = await _aiProvider.AnalyzeAsync(item.Title, item.TextContent, cancellationToken);
        if (!analysis.IsOpportunity)
            return null;

        // Check if opportunity already exists for this ContentItem
        var existing = await _dbContext.Opportunities
            .FirstOrDefaultAsync(o => o.ContentItemId == item.Id, cancellationToken);

        if (existing != null)
            return existing;

        var opportunityType = ParseOpportunityType(analysis.OpportunityType);
        var expiryDate = ExtractExpiryDate(item.TextContent ?? item.Title);
        var value = analysis.ExtractedValue ?? ExtractValue(item.TextContent ?? item.Title);

        var opportunity = new Opportunity
        {
            ContentItemId = item.Id,
            Title = item.Title,
            Description = item.Summary ?? item.TextContent,
            OpportunityType = opportunityType,
            Value = value,
            Currency = "USD",
            IsFree = true,
            Eligibility = "Developers",
            ExpiryDate = expiryDate,
            OfficialSourceUrl = item.Url,
            VerificationStatus = VerificationStatus.Verified,
            VerificationScore = 0.85,
            RelevanceScore = analysis.RelevanceScore,
            UrgencyScore = expiryDate.HasValue ? 0.7 : 0.2,
            Status = OpportunityStatus.Discovered
        };

        _dbContext.Opportunities.Add(opportunity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Discovered Opportunity: {Title} ({Type})", opportunity.Title, opportunity.OpportunityType);
        return opportunity;
    }

    private static OpportunityType ParseOpportunityType(string? typeStr) => typeStr switch
    {
        "FreeApiCredits" => OpportunityType.FreeApiCredits,
        "FreeCloudCredits" => OpportunityType.FreeCloudCredits,
        "FreeTrial" => OpportunityType.FreeTrial,
        "FreeSubscription" => OpportunityType.FreeSubscription,
        "OpenSourceRelease" => OpportunityType.OpenSourceRelease,
        "NewFreeTier" => OpportunityType.NewFreeTier,
        "DeveloperProgram" => OpportunityType.DeveloperProgram,
        _ => OpportunityType.FreeTool
    };

    private static DateTimeOffset? ExtractExpiryDate(string text)
    {
        // Simple regex matching dates like "until Oct 31", "expires 31 Dec 2026", "ends November 15"
        var match = ExpiryRegex().Match(text);
        if (match.Success && DateTimeOffset.TryParse(match.Groups[1].Value, out var date))
        {
            return date;
        }

        return null;
    }

    private static string? ExtractValue(string text)
    {
        var match = ValueRegex().Match(text);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"(?:until|expires|ends|before)\s+([A-Za-z]+\s+\d{1,2}(?:,\s+\d{4})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ExpiryRegex();

    [GeneratedRegex(@"\$\d+(?:,\d{3})*(?:\.\d{2})?", RegexOptions.Compiled)]
    private static partial Regex ValueRegex();
}
