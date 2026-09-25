using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;
using Signal.Application.Common.Models;
using Signal.Domain.Entities;
using Signal.Domain.Enums;

namespace Signal.Application.Services;

public class GoogleSheetApprovalService : IGoogleSheetsProvider
{
    private readonly ISignalDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<GoogleSheetApprovalService> _logger;

    public GoogleSheetApprovalService(
        ISignalDbContext dbContext,
        HttpClient httpClient,
        IConfiguration config,
        ILogger<GoogleSheetApprovalService> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<GoogleSheetSyncResult> SyncOpportunityAsync(
        Opportunity opportunity,
        CancellationToken cancellationToken = default)
    {
        var isEnabled = bool.TryParse(_config["GOOGLE_SHEETS_ENABLED"], out var enabled) && enabled;
        var webhookUrl = _config["GOOGLE_APPS_SCRIPT_URL"];


        // Prepare JSON payload
        var payload = new
        {
            opportunityId = opportunity.Id,
            title = opportunity.Title,
            category = "AI Opportunity",
            url = opportunity.OfficialSourceUrl ?? opportunity.ContentItem?.Url ?? string.Empty,
            canonicalUrl = opportunity.ContentItem?.CanonicalUrl ?? opportunity.OfficialSourceUrl ?? string.Empty,
            officialSourceUrl = opportunity.OfficialSourceUrl ?? string.Empty,
            opportunityType = opportunity.OpportunityType.ToString(),
            value = opportunity.Value ?? "Free",
            currency = opportunity.Currency ?? "USD",
            eligibility = opportunity.Eligibility ?? "All developers",
            expiryDate = opportunity.ExpiryDate?.ToString("yyyy-MM-dd"),
            verificationStatus = opportunity.VerificationStatus.ToString(),
            relevanceScore = opportunity.RelevanceScore,
            whyUseful = opportunity.Description
        };

        var payloadJson = JsonSerializer.Serialize(payload);

        // Record in SQLite first (Never lose an approved item!)
        var record = new GoogleSheetRecord
        {
            OpportunityId = opportunity.Id,
            StoryId = opportunity.StoryId,
            SheetName = "Opportunities",
            PayloadJson = payloadJson,
            Status = SyncStatus.PendingSync,
            RetryCount = 0
        };

        _dbContext.GoogleSheetRecords.Add(record);
        opportunity.Status = OpportunityStatus.Approved;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!isEnabled || string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogInformation("Google Sheets integration disabled or URL empty. Record queued locally for offline persistence.");
            return new GoogleSheetSyncResult(true, null, "Queued locally (Google Sheets Webhook disabled)");
        }

        // Attempt immediate synchronization
        try
        {
            var response = await _httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                var rowIdx = doc.TryGetProperty("row", out var r) ? r.GetInt32() : (int?)null;

                record.Status = SyncStatus.Synced;
                record.RowIndex = rowIdx;
                record.SyncedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully synced Opportunity '{Title}' to Google Sheets (Row #{Row})",
                    opportunity.Title, rowIdx);

                return new GoogleSheetSyncResult(true, rowIdx, null);
            }

            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            record.ErrorMessage = err;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new GoogleSheetSyncResult(false, null, $"HTTP Error: {response.StatusCode} - {err}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Google Apps Script webhook. Will retry in background.");
            record.ErrorMessage = ex.Message;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new GoogleSheetSyncResult(false, null, ex.Message);
        }
    }

    public async Task<int> ProcessRetryQueueAsync(CancellationToken cancellationToken = default)
    {
        var webhookUrl = _config["GOOGLE_APPS_SCRIPT_URL"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return 0;

        var pendingRecords = await _dbContext.GoogleSheetRecords
            .Where(r => r.Status == SyncStatus.PendingSync && r.RetryCount < 10)
            .OrderBy(r => r.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var syncedCount = 0;
        foreach (var rec in pendingRecords)
        {
            try
            {
                rec.RetryCount++;
                var payload = JsonSerializer.Deserialize<JsonElement>(rec.PayloadJson);
                var response = await _httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                    rec.RowIndex = doc.TryGetProperty("row", out var r) ? r.GetInt32() : null;
                    rec.Status = SyncStatus.Synced;
                    rec.SyncedAt = DateTimeOffset.UtcNow;
                    rec.ErrorMessage = null;
                    syncedCount++;
                }
                else
                {
                    rec.ErrorMessage = $"HTTP {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                rec.ErrorMessage = ex.Message;
            }
        }

        if (pendingRecords.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return syncedCount;
    }
}
