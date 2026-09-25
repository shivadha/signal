using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;
using Signal.Domain.Entities;
using Signal.Domain.Enums;

namespace Signal.Application.Services;

public record VerificationResult
{
    public VerificationStatus Status { get; init; }
    public double ConfidenceScore { get; init; }
    public string EvidenceSummary { get; init; } = string.Empty;
    public bool IsExpired { get; init; }
    public bool IsRecycledNews { get; init; }
}

public class VerificationService
{
    private readonly ISignalDbContext _dbContext;
    private readonly ILogger<VerificationService> _logger;

    public VerificationService(ISignalDbContext dbContext, ILogger<VerificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<VerificationResult> VerifyOpportunityAsync(
        Opportunity opportunity,
        CancellationToken cancellationToken = default)
    {
        // 1. Expiry Check
        if (opportunity.ExpiryDate.HasValue && opportunity.ExpiryDate.Value < DateTimeOffset.UtcNow)
        {
            opportunity.Status = OpportunityStatus.Expired;
            opportunity.VerificationStatus = VerificationStatus.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new VerificationResult
            {
                Status = VerificationStatus.Expired,
                ConfidenceScore = 1.0,
                EvidenceSummary = "Opportunity expiry date has elapsed.",
                IsExpired = true,
                IsRecycledNews = false
            };
        }

        // 2. Official Source Provenance
        var isOfficialDomain = false;
        if (!string.IsNullOrEmpty(opportunity.OfficialSourceUrl))
        {
            var uri = new Uri(opportunity.OfficialSourceUrl);
            var host = uri.Host.ToLowerInvariant();
            isOfficialDomain = host.Contains("openai.com") || host.Contains("anthropic.com")
                            || host.Contains("microsoft.com") || host.Contains("google.com")
                            || host.Contains("github.com") || host.Contains("aws.amazon.com")
                            || host.Contains("huggingface.co");
        }

        // 3. Recycled News Check (Look for older stories with similar title > 30 days old)
        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);
        var olderMatchingStory = await _dbContext.Stories
            .AsNoTracking()
            .Where(s => s.FirstDetectedAt < thirtyDaysAgo && s.Title.Contains(opportunity.Title))
            .FirstOrDefaultAsync(cancellationToken);

        var isRecycled = olderMatchingStory != null;
        var status = isOfficialDomain ? VerificationStatus.Verified : VerificationStatus.PartiallyVerified;
        var confidence = isOfficialDomain ? 0.95 : 0.65;

        if (isRecycled)
        {
            confidence -= 0.3;
        }

        opportunity.VerificationStatus = status;
        opportunity.VerificationScore = Math.Round(confidence, 2);
        opportunity.LastVerifiedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new VerificationResult
        {
            Status = status,
            ConfidenceScore = Math.Round(confidence, 2),
            EvidenceSummary = isOfficialDomain
                ? "Verified against official first-party publisher domain."
                : "Secondary source citation; primary provenance unconfirmed.",
            IsExpired = false,
            IsRecycledNews = isRecycled
        };
    }
}
