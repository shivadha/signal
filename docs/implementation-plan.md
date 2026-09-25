# SIGNAL — Multi-Phase Implementation Roadmap

This implementation plan details the staged development schedule for the Signal platform. Each phase adheres to strict quality gates: **Build → Test → Run → Verify → Document → Commit**.

---

## Phase Breakdown

### Phase 0: Foundation (Current)
- [x] Initialized Git repository on `main` and checked out `develop`.
- [x] Scaffolding of Clean Architecture .NET 10 solution:
  - `Signal.Domain`
  - `Signal.Application`
  - `Signal.Infrastructure`
  - `Signal.Api`
  - `Signal.Worker`
  - `Signal.UnitTests`
  - `Signal.IntegrationTests`
- [x] Multi-stage `Dockerfile`, `docker-compose.yml`, `.env.example`, `.gitignore`, `LICENSE`.
- [x] Comprehensive architectural and workflow documentation in `docs/`.
- [x] Verified zero-warning compilation and passing test suite.

### Phase 1: Ingestion & SQLite Persistence
- [ ] Implement core entities in `Signal.Domain`: `Source`, `ContentItem`.
- [ ] Implement EF Core DbContext with SQLite provider and migrations.
- [ ] Implement `ISourceProvider` and `RssSourceProvider` using `System.ServiceModel.Syndication`.
- [ ] Normalization engine: Strip UTM query parameters, normalize encoding, calculate SHA-256 hash.
- [ ] Level 1-3 deduplication: Exact URL, canonical URL, and content hash collision checks.
- [ ] Seed validated Tier-1 and Tier-2 AI feeds (OpenAI, Anthropic, Google DeepMind, Microsoft Developer, GitHub Blog).
- [ ] Unit tests for URL canonicalizer and hash deduplicator.

### Phase 2: Telegram Bot Interface
- [ ] Telegram Bot Client abstraction (`ITelegramProvider`).
- [ ] Command handlers: `/start`, `/today`, `/offers`, `/check <url>`, `/saved`, `/help`.
- [ ] Inline keyboard action handlers: Approve, Save for Later, Reject, Show Evidence.
- [ ] User authorization guard (restricting bot commands to configured `TELEGRAM_CHAT_ID`).

### Phase 3: Signal Scoring & AI Engine
- [ ] `IAIProvider` abstraction with fallback mechanism (`NoneAiProvider`, `OllamaAiProvider`, `GroqAiProvider`, `MockAiProvider`).
- [ ] Zero-cost deterministic heuristics: negative clickbait regex ("COMMENT AI", "DM ME", "99% DON'T KNOW").
- [ ] Scoring calculator: Source Quality, Evidence, Relevance, Freshness, Opportunity, Ragebait penalty.

### Phase 4: Verification & Expiry Monitor
- [ ] Verification engine matching claims to official Tier-1 documentation.
- [ ] Opportunity date parser & countdown alerts (5d, 2d, 1d, Last Day, Expired).
- [ ] Recycled news detector (flagging re-reporting of stale announcements).

### Phase 5: Story Clustering & Cross-Platform Deduplication
- [ ] `Story` and `StoryContent` domain entities and relationship mappings.
- [ ] Title tokenization & Levenshtein/Jaccard similarity clustering.
- [ ] New Information Differential: Detect when a newly ingested article adds net-new facts to an existing story.

### Phase 6: YouTube Ingestion & Video Analysis
- [ ] YouTube channel RSS parser and video metadata extractor.
- [ ] Video transcript retrieval (public closed captions).
- [ ] Information density scoring (filtering high-volume commentary in favor of dense technical tutorials).

### Phase 7: Google Sheets Approval Gateway
- [ ] `GoogleSheetApprovalService` with Google Apps Script Web App HTTPS webhook.
- [ ] Local persistence of approval events in `GoogleSheetRecord`.
- [ ] Offline queue & resilient background retry mechanism.
- [ ] Idempotency checks preventing duplicate rows in spreadsheet.

### Phase 8: Personalization & Continuous Learning
- [ ] User profile entity with configurable topic weights.
- [ ] Feedback ingestion (`👍 Useful`, `👎 Not Useful`, `🚫 Mute Source`, `🚫 Mute Topic`).
- [ ] Dynamic scoring adjustments based on user interaction history.

### Phase 9: Authorized Social Content Ingestion
- [ ] User-submitted Reel and TikTok link analysis via public metadata and URL canonicalization.
- [ ] Public Telegram channel ingestion support.

### Phase 10: iOS Client & TestFlight Preparation
- [ ] RESTful API completion for mobile consumption.
- [ ] Swift / SwiftUI client architecture and TestFlight deployment.
