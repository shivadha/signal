using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;

namespace Signal.Infrastructure.Providers.AI;

public class OllamaAiProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<OllamaAiProvider> _logger;
    private readonly NoneAiProvider _fallback;

    public OllamaAiProvider(HttpClient httpClient, IConfiguration config, ILogger<OllamaAiProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _fallback = new NoneAiProvider();
    }

    public string ProviderName => "Ollama Local LLM";

    public async Task<AIAnalysisResult> AnalyzeAsync(string title, string? text, CancellationToken cancellationToken = default)
    {
        var endpoint = _config["AI_ENDPOINT"] ?? "http://localhost:11434";
        var model = _config["AI_MODEL_NAME"] ?? "llama3.2";

        try
        {
            var prompt = $$"""
                Analyze the following content for a software/AI engineer:
                Title: {{title}}
                Snippet: {{text}}

                Return JSON only in this format:
                {
                  "isRelevant": true/false,
                  "relevanceScore": 0.0 to 1.0,
                  "rageBaitScore": 0.0 to 1.0,
                  "isOpportunity": true/false,
                  "opportunityType": "FreeApiCredits" | "FreeTool" | "OpenSourceRelease" | null,
                  "summary": "Brief 1-sentence summary"
                }
                """;


            var payload = new
            {
                model,
                prompt,
                stream = false,
                format = "json"
            };

            var response = await _httpClient.PostAsJsonAsync($"{endpoint}/api/generate", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                var responseText = jsonDoc.GetProperty("response").GetString();
                if (!string.IsNullOrEmpty(responseText))
                {
                    var parsed = JsonSerializer.Deserialize<JsonElement>(responseText);
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
            _logger.LogWarning(ex, "Ollama local API unavailable. Falling back to deterministic rule engine.");
        }

        return await _fallback.AnalyzeAsync(title, text, cancellationToken);
    }
}
