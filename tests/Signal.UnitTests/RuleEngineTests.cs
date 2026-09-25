using Signal.Infrastructure.Providers.AI;
using Xunit;

namespace Signal.UnitTests;

public class RuleEngineTests
{
    private readonly NoneAiProvider _ruleEngine = new();

    [Fact]
    public async Task AnalyzeAsync_DetectsRageBaitPatterns()
    {
        // Arrange
        var title = "YOU WON'T BELIEVE THIS! COMMENT AI TO GET SECRET TRICK NOW!";
        var content = "This changes everything. 99% of people don't know.";

        // Act
        var result = await _ruleEngine.AnalyzeAsync(title, content);

        // Assert
        Assert.True(result.RageBaitScore >= 0.7, $"Expected RageBaitScore >= 0.7, but got {result.RageBaitScore}");
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsFreeOpportunity()
    {
        // Arrange
        var title = "Anthropic announces free credits for developer accounts";
        var content = "Eligible developers can claim API credits.";

        // Act
        var result = await _ruleEngine.AnalyzeAsync(title, content);

        // Assert
        Assert.True(result.IsOpportunity);
        Assert.Equal("FreeApiCredits", result.OpportunityType);
        Assert.True(result.RelevanceScore >= 0.7);
    }

    [Fact]
    public async Task AnalyzeAsync_IdentifiesLowRelevanceNonAiContent()
    {
        // Arrange
        var title = "Top 10 summer cookie recipes for 2026";
        var content = "Bake delicious desserts with flour and sugar.";

        // Act
        var result = await _ruleEngine.AnalyzeAsync(title, content);

        // Assert
        Assert.False(result.IsRelevant);
        Assert.True(result.RelevanceScore < 0.55);
    }
}
