using Signal.Application.Services;
using Xunit;

namespace Signal.UnitTests;

public class NormalizerTests
{
    private readonly ContentNormalizationService _normalizer = new();

    [Fact]
    public void CanonicalizeUrl_StripsTrackingParameters_PreservesLegitimateQuery()
    {
        // Arrange
        var dirtyUrl = "https://WWW.YouTube.com/watch?v=dQw4w9WgXcQ&utm_source=twitter&utm_medium=social&utm_campaign=launch&fbclid=IwAR123";

        // Act
        var cleanUrl = _normalizer.CanonicalizeUrl(dirtyUrl);

        // Assert
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", cleanUrl);
    }

    [Fact]
    public void CanonicalizeUrl_WhenOnlyTrackingParameters_ReturnsBaseUrlWithoutQuery()
    {
        // Arrange
        var dirtyUrl = "https://openai.com/news/swarm/?utm_source=reddit&ref=newsletter";

        // Act
        var cleanUrl = _normalizer.CanonicalizeUrl(dirtyUrl);

        // Assert
        Assert.Equal("https://openai.com/news/swarm", cleanUrl);
    }

    [Fact]
    public void ComputeContentHash_ProducesDeterministicSha256()
    {
        // Arrange
        var title = "OpenAI Releases Swarm";
        var content = "Educational multi-agent framework";

        // Act
        var hash1 = _normalizer.ComputeContentHash(title, content);
        var hash2 = _normalizer.ComputeContentHash("  openai releases swarm ", "educational multi-agent framework  ");

        // Assert
        Assert.Equal(64, hash1.Length);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void CleanText_StripsHtmlTagsAndDecodesEntities()
    {
        // Arrange
        var rawHtml = "<p>Discover <b>Claude &amp; GPT-4o</b> updates at &quot;Scale&quot;.</p>";

        // Act
        var cleaned = _normalizer.CleanText(rawHtml);

        // Assert
        Assert.Equal("Discover Claude & GPT-4o updates at \"Scale\".", cleaned);
    }
}
