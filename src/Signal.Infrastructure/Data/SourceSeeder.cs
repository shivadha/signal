using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signal.Domain.Entities;
using Signal.Domain.Enums;
using Signal.Infrastructure.Persistence;

namespace Signal.Infrastructure.Data;

public static class SourceSeeder
{
    public static async Task SeedAsync(SignalDbContext context, ILogger logger)
    {
        var defaultSources = new List<Source>
        {
            new()
            {
                Name = "OpenAI News",
                Url = "https://openai.com/news/",
                FeedUrl = "https://openai.com/news/rss.xml",
                SourceType = SourceType.OfficialBlog,
                Category = "AI Research & Models",
                TrustTier = TrustTier.Tier1_Official,
                IsOfficial = true,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "Google DeepMind Blog",
                Url = "https://deepmind.google/blog/",
                FeedUrl = "https://deepmind.google/blog/rss.xml",
                SourceType = SourceType.OfficialBlog,
                Category = "AI Research & Models",
                TrustTier = TrustTier.Tier1_Official,
                IsOfficial = true,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "Microsoft .NET Blog",
                Url = "https://devblogs.microsoft.com/dotnet/",
                FeedUrl = "https://devblogs.microsoft.com/dotnet/feed/",
                SourceType = SourceType.OfficialBlog,
                Category = ".NET & AI Tooling",
                TrustTier = TrustTier.Tier1_Official,
                IsOfficial = true,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "Microsoft Developer Blog",
                Url = "https://devblogs.microsoft.com/",
                FeedUrl = "https://devblogs.microsoft.com/feed/",
                SourceType = SourceType.OfficialBlog,
                Category = "Developer Tools",
                TrustTier = TrustTier.Tier1_Official,
                IsOfficial = true,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "GitHub Blog",
                Url = "https://github.blog/",
                FeedUrl = "https://github.blog/feed/",
                SourceType = SourceType.OfficialBlog,
                Category = "Developer Tools & AI",
                TrustTier = TrustTier.Tier1_Official,
                IsOfficial = true,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "GitHub Changelog",
                Url = "https://github.blog/changelog/",
                FeedUrl = "https://github.blog/changelog/feed/",
                SourceType = SourceType.OfficialBlog,
                Category = "Developer Tools & AI",
                TrustTier = TrustTier.Tier1_Official,
                IsOfficial = true,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "AWS Machine Learning Blog",
                Url = "https://aws.amazon.com/blogs/machine-learning/",
                FeedUrl = "https://aws.amazon.com/blogs/machine-learning/feed/",
                SourceType = SourceType.OfficialBlog,
                Category = "Cloud AI & Infrastructure",
                TrustTier = TrustTier.Tier1_Official,
                IsOfficial = true,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "Hugging Face Blog",
                Url = "https://huggingface.co/blog",
                FeedUrl = "https://huggingface.co/blog/feed.xml",
                SourceType = SourceType.OfficialBlog,
                Category = "Open-Source Models",
                TrustTier = TrustTier.Tier1_Official,
                IsOfficial = true,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "Reddit - r/freebies (Deals & Offers)",
                Url = "https://www.reddit.com/r/freebies/",
                FeedUrl = "https://www.reddit.com/r/freebies/new/.rss",
                SourceType = SourceType.Rss,
                Category = "Deals & Freebies",
                TrustTier = TrustTier.Tier4_Community,
                IsOfficial = false,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "Reddit - r/webdev (Developer Tools)",
                Url = "https://www.reddit.com/r/webdev/",
                FeedUrl = "https://www.reddit.com/r/webdev/new/.rss",
                SourceType = SourceType.Rss,
                Category = "Developer Tools",
                TrustTier = TrustTier.Tier4_Community,
                IsOfficial = false,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "Reddit - r/LocalLLaMA (Open-Source AI)",
                Url = "https://www.reddit.com/r/LocalLLaMA/",
                FeedUrl = "https://www.reddit.com/r/LocalLLaMA/new/.rss",
                SourceType = SourceType.Rss,
                Category = "Open-Source Models",
                TrustTier = TrustTier.Tier4_Community,
                IsOfficial = false,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "Reddit - r/OpenAI (AI Releases & News)",
                Url = "https://www.reddit.com/r/OpenAI/",
                FeedUrl = "https://www.reddit.com/r/OpenAI/new/.rss",
                SourceType = SourceType.Rss,
                Category = "AI Research & Models",
                TrustTier = TrustTier.Tier4_Community,
                IsOfficial = false,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "Hacker News (New & Tech)",
                Url = "https://news.ycombinator.com/",
                FeedUrl = "https://news.ycombinator.com/rss",
                SourceType = SourceType.Rss,
                Category = "Tech & Startups",
                TrustTier = TrustTier.Tier3_Specialist,
                IsOfficial = false,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "YouTube - Fireship (Code & Tools)",
                Url = "https://www.youtube.com/@Fireship",
                FeedUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCsBjURrPoezykLs9EqgamOA",
                SourceType = SourceType.YouTubeChannel,
                Category = "Developer Tools & Code",
                TrustTier = TrustTier.Tier3_Specialist,
                IsOfficial = false,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "YouTube - Matt Wolfe (AI Tools & News)",
                Url = "https://www.youtube.com/@mreflow",
                FeedUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCn5WodWzZkKvdz9jJ5G2q5w",
                SourceType = SourceType.YouTubeChannel,
                Category = "AI Tools & Opportunities",
                TrustTier = TrustTier.Tier3_Specialist,
                IsOfficial = false,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "YouTube - Y Combinator",
                Url = "https://www.youtube.com/@ycombinator",
                FeedUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCcefcZRL2oaA_TsBiDDLu-g",
                SourceType = SourceType.YouTubeChannel,
                Category = "Tech & Startups",
                TrustTier = TrustTier.Tier2_Established,
                IsOfficial = true,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "YouTube - Two Minute Papers",
                Url = "https://www.youtube.com/@TwoMinutePapers",
                FeedUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCbfYPyITQ-7l4upoX8nvctg",
                SourceType = SourceType.YouTubeChannel,
                Category = "AI Research & Models",
                TrustTier = TrustTier.Tier3_Specialist,
                IsOfficial = false,
                IsActive = true,
                Language = "en"
            },
            new()
            {
                Name = "Reddit - r/selfhosted (Open-Source Tools)",
                Url = "https://www.reddit.com/r/selfhosted/",
                FeedUrl = "https://www.reddit.com/r/selfhosted/new/.rss",
                SourceType = SourceType.Reddit,
                Category = "Developer Tools",
                TrustTier = TrustTier.Tier4_Community,
                IsOfficial = false,
                IsActive = true,
                Language = "en",
                Country = "US"
            },
            // --- Chinese AI Platforms & Communities (Auto-translated to English) ---
            new()
            {
                Name = "DeepSeek AI (China Official Releases)",
                Url = "https://github.com/deepseek-ai",
                FeedUrl = "https://github.com/deepseek-ai/DeepSeek-V2/releases.atom",
                SourceType = SourceType.OfficialBlog,
                Category = "Chinese AI & Models",
                TrustTier = TrustTier.Tier1_Official,
                IsOfficial = true,
                IsActive = true,
                Language = "zh",
                Country = "CN"
            },
            new()
            {
                Name = "Qwen AI / Alibaba Cloud (Releases)",
                Url = "https://github.com/QwenLM",
                FeedUrl = "https://github.com/QwenLM/Qwen/releases.atom",
                SourceType = SourceType.OfficialBlog,
                Category = "Chinese AI & Models",
                TrustTier = TrustTier.Tier1_Official,
                IsOfficial = true,
                IsActive = true,
                Language = "zh",
                Country = "CN"
            },
            new()
            {
                Name = "Solidot (China Tech & Open Source)",
                Url = "https://www.solidot.org/",
                FeedUrl = "https://www.solidot.org/index.rss",
                SourceType = SourceType.Rss,
                Category = "Chinese Tech & Tools",
                TrustTier = TrustTier.Tier3_Specialist,
                IsOfficial = false,
                IsActive = true,
                Language = "zh",
                Country = "CN"
            },
            // --- Japanese AI Platforms & Communities (Auto-translated to English) ---
            new()
            {
                Name = "Hatena Bookmark IT & AI (Japan)",
                Url = "https://b.hatena.ne.jp/hotentry/it",
                FeedUrl = "https://b.hatena.ne.jp/hotentry/it.rss",
                SourceType = SourceType.Rss,
                Category = "Japanese AI & Tech",
                TrustTier = TrustTier.Tier3_Specialist,
                IsOfficial = false,
                IsActive = true,
                Language = "ja",
                Country = "JP"
            },
            new()
            {
                Name = "Qiita AI Developer Feed (Japan)",
                Url = "https://qiita.com/tags/ai",
                FeedUrl = "https://qiita.com/tags/ai/feed.atom",
                SourceType = SourceType.Rss,
                Category = "Japanese AI & Tech",
                TrustTier = TrustTier.Tier3_Specialist,
                IsOfficial = false,
                IsActive = true,
                Language = "ja",
                Country = "JP"
            },
            new()
            {
                Name = "Zenn AI Engineering Feed (Japan)",
                Url = "https://zenn.dev/topics/ai",
                FeedUrl = "https://zenn.dev/topics/ai/feed",
                SourceType = SourceType.Rss,
                Category = "Japanese AI & Tech",
                TrustTier = TrustTier.Tier3_Specialist,
                IsOfficial = false,
                IsActive = true,
                Language = "ja",
                Country = "JP"
            }
        };

        int addedSources = 0;
        foreach (var src in defaultSources)
        {
            var exists = await context.Sources.AnyAsync(s => s.FeedUrl == src.FeedUrl || s.Url == src.Url);
            if (!exists)
            {
                await context.Sources.AddAsync(src);
                addedSources++;
            }
        }

        if (addedSources > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Added {AddedCount} new active sources into database.", addedSources);
        }

        // Seed initial default user preferences if none exist
        if (!await context.UserPreferences.AnyAsync())
        {
            var defaultPreferences = new List<UserPreference>
            {
                new() { Key = "AI", Weight = 10, Category = "Core" },
                new() { Key = "LLM", Weight = 10, Category = "Core" },
                new() { Key = "AI Agents", Weight = 10, Category = "Core" },
                new() { Key = "AI Coding", Weight = 10, Category = "Core" },
                new() { Key = ".NET AI", Weight = 9, Category = "Ecosystem" },
                new() { Key = "OpenAI", Weight = 9, Category = "Provider" },
                new() { Key = "Claude", Weight = 9, Category = "Provider" },
                new() { Key = "Gemini", Weight = 8, Category = "Provider" },
                new() { Key = "MCP", Weight = 9, Category = "Architecture" },
                new() { Key = "Developer Tools", Weight = 10, Category = "Opportunity" },
                new() { Key = "Free AI Tools", Weight = 10, Category = "Opportunity" },
                new() { Key = "API Credits", Weight = 10, Category = "Opportunity" },
                new() { Key = "Cloud Credits", Weight = 9, Category = "Opportunity" },
                new() { Key = "AI Video", Weight = 3, Category = "Media" },
                new() { Key = "AI Music", Weight = 2, Category = "Media" },
                new() { Key = "Generic Influencer Content", Weight = 1, Category = "Social" }
            };

            await context.UserPreferences.AddRangeAsync(defaultPreferences);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded default user preferences.");
        }
    }

}
