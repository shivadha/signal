# SIGNAL

> **Your personal information firewall. Signal over noise.**

Signal is an open-source, zero-cost intelligence, developer opportunity, and social signal platform built with .NET 10 and ASP.NET Core. It monitors the internet—AI blogs, RSS feeds, YouTube channels, developer programs, and social discovery feeds—to filter out clickbait, rage-bait, and repetitive reposts, bringing only verified, high-value AI releases, free developer tools, and limited-time opportunities straight to your Telegram and personal Google Sheet tracker.

---

## 1. Why Signal Exists

The internet is flooded with AI news, tutorials, and promotional videos. On YouTube, Reddit, Instagram Reels, and TikTok, dozens of creators repost identical talking points using sensationalized headlines (*"THIS CHANGES EVERYTHING"*, *"COMMENT AI TO GET ACCESS"*, *"THEY ARE HIDING THIS"*).

Signal changes the paradigm:
- **No Endless Feeds:** Never scroll a feed again. If 10,000 items are published and only 3 contain genuine signal for you, Signal sends only those 3.
- **Cross-Platform Story Clustering:** 50 YouTube videos and 20 articles discussing the same release are collapsed into **1 Story**, linking the best technical video and official source.
- **Verification Before Hype:** Social claims are treated as discovery leads, not proof. Signal verifies announcements against official documentation and releases before alerting you.
- **Opportunity Detection:** Automatically surfaces free API credits, cloud tier announcements, developer tools, and open-source models, calculating urgency and expiry dates.
- **Authoritative Approval Gateway:** Signal never writes unvetted data to your personal trackers. Only when you explicitly tap **[Approve & Save]** in Telegram does the structured data persist to your Google Sheet.

---

## 2. System Architecture

Signal is built as a modular monolith adhering to Clean Architecture principles:

```text
                             +------------------------+
                             |   Upstream Sources     |
                             |  RSS / YouTube / Blogs |
                             +-----------+------------+
                                         |
                                         v
                             +------------------------+
                             |  Ingestion Engine      |
                             |  (Resilient Polling)   |
                             +-----------+------------+
                                         |
                                         v
+-----------------------+    +------------------------+
| Level 1-4 Dedupe      |    | Canonicalization &     |
| (URL, Hash, Jaccard)  |<-->| Normalization          |
+-----------------------+    +-----------+------------+
                                         |
                                         v
+-----------------------+    +------------------------+
| Multi-Platform Story  |<---| Story Clustering       |
| Consolidation         |    | (Cross-Source Engine)  |
+-----------------------+    +-----------+------------+
                                         |
                                         v
+-----------------------+    +------------------------+
| IAIProvider           |<-->| Signal Scoring Engine  |
| (Ollama/Groq/Gemini)  |    | (Relevance & Quality)  |
+-----------------------+    +-----------+------------+
                                         |
                                         v
                             +------------------------+
                             | Telegram Bot Interface |
                             | (Actionable Cards)     |
                             +-----------+------------+
                                         |
                                         | [Approve & Save]
                                         v
                             +------------------------+
                             | Google Sheets Webhook  |
                             | (Resilient Sync Queue) |
                             +------------------------+
```

---

## 3. Technology Stack & Design Decisions

- **Runtime & Language:** .NET 10 (C# 14), offering top-tier performance, native memory efficiency, and rock-solid asynchronous I/O.
- **Architecture:** Modular Monolith (Clean Architecture) avoiding unnecessary microservices overhead for single-user and small-team deployments.
- **Persistence:** Entity Framework Core with SQLite for zero-cost, serverless single-file operations, portable to PostgreSQL.
- **Background Orchestration:** .NET Worker Service (`Signal.Worker`) executing non-blocking scheduled pipelines.
- **API Surface:** ASP.NET Core Minimal APIs for lightweight inspection and mobile app client consumption.
- **Containerization:** Multi-stage, non-root secure Docker images.

---

## 4. Zero-Cost ($0/Month) Strategy

Signal is specifically engineered to run entirely free of charge:
- **Database:** Local SQLite file (`signal.db`) requiring no external database subscription.
- **Compute:** Capable of running on local hardware, a home server, Raspberry Pi, or free tier container hosting.
- **AI Processing:** Operates seamlessly with `AI_PROVIDER=none` using regex heuristics, trust weighting, and fuzzy title matching. Optionally hooks into local **Ollama** or free tiers of Groq and Gemini.
- **Spreadsheet Sync:** Uses a serverless Google Apps Script Web App requiring no paid GCP developer accounts or OAuth verification fees.

---

## 5. Getting Started (Local Setup)

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

### Installation
```bash
# Clone the repository
git clone https://github.com/shivadha/signal.git
cd signal

# Switch to develop branch
git checkout develop

# Copy environment configuration
cp .env.example .env

# Restore dependencies and build
dotnet restore
dotnet build

# Execute test suite
dotnet test
```

### Running the API & Worker
```bash
# Run the API
dotnet run --project src/Signal.Api

# In a separate terminal, run the background worker
dotnet run --project src/Signal.Worker
```

---

## 6. Docker Deployment

Launch Signal with persistent SQLite storage in one command:

```bash
docker compose up -d
```

Check API health:
```bash
curl http://localhost:8080/health
```

---

## 7. Telegram Bot Setup

1. Open Telegram and message `@BotFather` to create a new bot. Copy the bot token.
2. Message your new bot and get your numeric Chat ID (e.g. using `@userinfobot`).
3. Set the variables in your `.env` file:
   ```env
   TELEGRAM_BOT_TOKEN=123456789:ABCdefGhIJKlmNoPQRstuVWXyz
   TELEGRAM_CHAT_ID=987654321
   ```
4. Start the application. The bot responds to `/start`, `/today`, `/offers`, `/check <url>`, and interactive approval buttons.

---

## 8. Google Sheets Approval Setup

Signal connects to Google Sheets via a lightweight Google Apps Script Web App:

1. Open your target Google Sheet.
2. Navigate to **Extensions > Apps Script**.
3. Copy the script provided in `docs/google-sheets-workflow.md` (or `deploy/google-apps-script.js`) into the editor.
4. Click **Deploy > New deployment**, select type **Web App**, set access to **Anyone**, and click **Deploy**.
5. Copy the generated Web App URL and add it to your `.env`:
   ```env
   GOOGLE_SHEETS_ENABLED=true
   GOOGLE_APPS_SCRIPT_URL=https://script.google.com/macros/s/AKfycbx.../exec
   ```
6. When you click **[✅ APPROVE & SAVE]** in Telegram, Signal formats and appends the opportunity to your sheet immediately. If offline, Signal queues the row locally and retries automatically.

---

## 9. Verification & Scoring Engine

Each incoming discovery is assigned a normalized `FinalScore` (0.0 to 1.0):

$$\text{FinalScore} = (0.20 \times \text{Quality}) + (0.20 \times \text{Evidence}) + (0.25 \times \text{Relevance}) + (0.15 \times \text{Freshness}) + (0.15 \times \text{Opportunity}) + (0.05 \times \text{Urgency}) - \text{Penalties}$$

- **Rage-Bait Penalty:** Downweights hyperbolic, unverified phrases (*"You won't believe"*, *"99% don't know"*, *"Secret hack"*).
- **Duplicate Penalty:** Drops items when an existing Story cluster already covers the same announcement with equal or greater provenance.

---

## 10. Development Roadmap

- [x] **Phase 0: Foundation:** Solution architecture, Clean Architecture scaffolding, Docker, tests, and documentation.
- [ ] **Phase 1: Source & Database:** SQLite EF Core context, RSS feed ingest, URL canonicalization, and seed sources.
- [ ] **Phase 2: Telegram Bot:** Telegram client, interactive buttons, and `/check <url>` handler.
- [ ] **Phase 3: AI Engine:** Heuristics, IAIProvider abstractions (Ollama, Groq, Gemini), and scoring.
- [ ] **Phase 4: Verification:** Claim verification against official sources and opportunity expiry tracker.
- [ ] **Phase 5: Story Engine:** Cross-source clustering and new information delta detector.
- [ ] **Phase 6: YouTube Ingestion:** YouTube channel feeds, transcript extraction, and watch score.
- [ ] **Phase 7: Google Sheets Gateway:** Resilient offline queue and Apps Script bridge.
- [ ] **Phase 8: Personalization:** User feedback tracking and score adjustments.
- [ ] **Phase 9: Social Discovery:** Supported public metadata analysis for Instagram & TikTok links.
- [ ] **Phase 10: iOS Client:** SwiftUI native client for TestFlight distribution.

---

## 11. Contributing & License

Contributions are welcome! Please submit PRs against the `develop` branch.

Licensed under the [MIT License](LICENSE).
