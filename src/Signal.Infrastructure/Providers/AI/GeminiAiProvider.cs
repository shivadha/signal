using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;

namespace Signal.Infrastructure.Providers.AI;

public class GeminiAiProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiAiProvider> _logger;
    private readonly NoneAiProvider _fallback;

    public GeminiAiProvider(HttpClient httpClient, IConfiguration config, ILogger<GeminiAiProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _fallback = new NoneAiProvider();
    }

    public string ProviderName => "Google Gemini Free Tier";

    public async Task<AIAnalysisResult> AnalyzeAsync(string title, string? text, CancellationToken cancellationToken = default)
    {
        var apiKey = _config["AI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return await _fallback.AnalyzeAsync(title, text, cancellationToken);
        }

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
            var prompt = $$"""
                Analyze the following content for relevance to a software engineer and AI practitioner:
                Title: {{title}}
                Snippet: {{text}}

                Respond ONLY with raw JSON matching this schema:
                {
                  "isRelevant": true,
                  "relevanceScore": 0.85,
                  "rageBaitScore": 0.1,
                  "isOpportunity": false,
                  "opportunityType": null,
                  "summary": "one sentence summary"
                }
                """;


            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                var rawJson = doc.GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                if (!string.IsNullOrEmpty(rawJson))
                {
                    // Clean markdown code blocks if wrapped
                    var cleanJson = rawJson.Replace("```json", "").Replace("```", "").Trim();
                    var parsed = JsonSerializer.Deserialize<JsonElement>(cleanJson);

                    return new AIAnalysisResult
                    {
                        IsRelevant = parsed.GetProperty("isRelevant").GetBoolean(),
                        RelevanceScore = parsed.GetProperty("relevanceScore").GetDouble(),
                        RageBaitScore = parsed.GetProperty("rageBaitScore").GetDouble(),
                        IsOpportunity = parsed.GetProperty("isOpportunity").GetBoolean(),
                        OpportunityType = parsed.TryGetProperty("opportunityType", out var ot) && ot.ValueKind != JsonValueKind.Null ? ot.GetString() : null,
                        Summary = parsed.TryGetProperty("summary", out var sm) ? sm.GetString() : title,
                        Claims = new List<string> { title }
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini API failed. Falling back to deterministic rule engine.");
        }

        return await _fallback.AnalyzeAsync(title, text, cancellationToken);
    }
}
