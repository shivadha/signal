# SIGNAL

> **Your personal information firewall. Signal over noise.**

Signal is an open-source, zero-cost intelligence, developer opportunity, and social signal platform built with .NET 10 and ASP.NET Core. It monitors the internet—AI blogs, RSS feeds, YouTube channels, developer programs, and social discovery feeds—to filter out clickbait, rage-bait, and repetitive reposts, bringing only verified, high-value AI releases, free developer tools, and limited-time opportunities straight to your Telegram and personal Google Sheet tracker.

---

## 1. Key Capabilities & Features

- **24/7 Autonomous Ingestion (Every 30 Minutes):**
  Continuously monitors US, Chinese, and Japanese platforms (OpenAI, DeepSeek, Qwen, Anthropic, Google DeepMind, GitHub, Qiita, Hatena, Zenn, Reddit r/LocalLLaMA, r/freebies).
- **Strict English-Only Guarantee (100% Translated):**
  Multi-engine translation (Google GTX, Chrome Dict API, MyMemory, Lingva) auto-detects foreign scripts (Chinese, Japanese, Korean, Russian, Arabic) and translates them into clear English. Zero foreign characters ever reach Telegram.
- **Topic-Segregated Intelligence Digest:**
  Automatically categorizes discoveries into distinct topics with explanatory context and image previews:
  - 🤖 **AI NEWS & OFFERS**
  - 🛠️ **SOFTWARE & DEVELOPER TOOLS**
  - 🎁 **FREEBIES & DEALS**
  - 📰 **TECH ECOSYSTEM & INNOVATION**
- **GitHub Repository Extraction & Direct Task Usage:**
  Extracts GitHub repositories from video transcripts, descriptions, articles, and websites. Includes direct repository links and interactive buttons to use and inspect code immediately.
- **YouTube & Instagram Reel Legitimacy Verifier (`/verify <url>`):**
  Pulls full captions and transcripts from YouTube and Instagram Reels. Detects bypass scams, malware downloads (.zip/.exe), and fake claims.
- **Two-Tab Google Sheets Integration:**
  - `Opportunities` tab: Saves approved API credits, cloud grants, and free tiers.
  - `Tools` tab: Saves trending open-source developer tools, CLI utilities, and GitHub repositories.

---

## 2. Interactive Telegram Bot Controls

| Command | Action |
|---|---|
| `/today` | Curated discoveries segregated by topic with image previews and context |
| `/offers` | Free AI credits, API grants, and discounts (Claude, OpenAI, Gemini, Cursor, Muse) |
| `/tools` | Trending open-source developer tools, CLI utilities & GitHub repos |
| `/global` | International AI releases (DeepSeek, Qwen, Qiita, Hatena) auto-translated to English |
| `/verify <url>` | Deep verification of YouTube, Instagram Reel, or article link for scams & legitimacy |
| `/transcript <url>` | Extract full timestamped audio transcript or video script |
| `/saved` | View your personal approved library |

---

## 3. System Architecture

```text
                             +------------------------+
                             |   Upstream Sources     |
                             |  RSS / YouTube / Blogs |
                             +-----------+------------+
                                         |
                                         v
                             +------------------------+
                             |  Ingestion Engine      |
                             |  (30-Min Resilient)    |
                             +-----------+------------+
                                         |
                                         v
+-----------------------+    +------------------------+
| Level 1-4 Dedupe      |    | Canonicalization &     |
| (URL, Hash, Jaccard)  |<-->| Multi-Tier Translation |
+-----------------------+    +-----------+------------+
                                         |
                                         v
+-----------------------+    +------------------------+
| Multi-Platform Story  |<---| Video Inspection &     |
| Consolidation         |    | Transcript Extraction  |
+-----------------------+    +-----------+------------+
                                         |
                                         v
+-----------------------+    +------------------------+
| GitHub Extractor      |<-->| Signal Scoring Engine  |
| (Repo Discovery)      |    | (Relevance & Quality)  |
+-----------------------+    +-----------+------------+
                                         |
                                         v
                             +------------------------+
                             | Telegram Bot Interface |
                             | (Topic Digest & Cards) |
                             +-----------+------------+
                                         |
                                         | [Approve & Save]
                                         v
                             +------------------------+
                             | Google Sheets (2 Tabs) |
                             | Opportunities & Tools  |
                             +------------------------+
```

---

## 4. Zero-Cost ($0/Month) Strategy

Signal is engineered to run entirely free of charge:
- **Database:** Local SQLite file (`signal.db`) requiring no external database subscription.
- **Compute:** Capable of running on local hardware, a home server, Raspberry Pi, or free tier container hosting.
- **AI Processing:** Operates seamlessly with `AI_PROVIDER=none` using regex heuristics, trust weighting, and fuzzy title matching. Optionally hooks into local **Ollama** or free tiers of Groq and Gemini.
- **Translation:** 4-tier free translation engine requiring zero paid API keys.
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

# Switch to develop or main branch
git checkout main

# Copy environment configuration
cp .env.example .env

# Restore dependencies and build
dotnet restore
dotnet build

# Execute test suite
dotnet test
```

### Running the API & Background Worker
```bash
# Run the API (hosts Webhook, API, Worker, and Telegram Polling)
dotnet run --project src/Signal.Api

# Or run the Worker standalone
dotnet run --project src/Signal.Worker
```

---

## 6. Docker Deployment ($0/Month)

Launch Signal with persistent SQLite storage in one command:

```bash
docker compose up -d
```

Check API health:
```bash
curl http://localhost:8080/health
```

---

## 7. Cloud Deployment (Free 24/7 Hosting)

For step-by-step instructions on deploying to **Render.com**, **Koyeb**, **Fly.io**, or **Windows Silent Runner**, see:
📘 [Free Deployment Guide](docs/free-deployment-guide.md)

---

## 8. Telegram Bot Setup

1. Open Telegram and message `@BotFather` to create a new bot. Copy the bot token.
2. Message your new bot and get your numeric Chat ID (e.g. using `@userinfobot`).
3. Set the variables in your `.env` file:
   ```env
   TELEGRAM_BOT_TOKEN=your_bot_token_here
   TELEGRAM_CHAT_ID=your_chat_id_here
   ```
4. Start the application. The bot responds to `/start`, `/today`, `/offers`, `/tools`, `/global`, `/verify <url>`, and interactive approval buttons.

---

## 9. Google Sheets 2-Tab Setup

Signal connects to Google Sheets via a lightweight Google Apps Script Web App:

1. Open your target Google Sheet and create two tabs: `Opportunities` and `Tools`.
2. Navigate to **Extensions > Apps Script**.
3. Copy the script provided in `deploy/google-apps-script.js` into the editor.
4. Click **Deploy > New deployment**, select type **Web App**, set access to **Anyone**, and click **Deploy**.
5. Copy the generated Web App URL and add it to your `.env`:
   ```env
   GOOGLE_SHEETS_ENABLED=true
   GOOGLE_APPS_SCRIPT_URL=https://script.google.com/macros/s/AKfycbx.../exec
   ```
6. When you click **[✅ APPROVE & SAVE]** or **[⭐ SAVE TOOL TO SHEET]** in Telegram, Signal formats and appends the item to the respective tab immediately.

---

## 10. Development Roadmap

- [x] **Phase 0: Foundation:** Solution architecture, Clean Architecture scaffolding, Docker, tests, and documentation.
- [x] **Phase 1: Source & Database:** SQLite EF Core context, RSS feed ingest, URL canonicalization, and seed sources.
- [x] **Phase 2: Telegram Bot:** Telegram client, interactive buttons, `/today`, `/offers`, `/tools`, `/global`, `/verify`, and `/saved`.
- [x] **Phase 3: AI Engine:** Heuristics, IAIProvider abstractions (Ollama, Groq, Gemini), and scoring.
- [x] **Phase 4: Verification:** Claim verification against official sources, scam detection, and opportunity expiry tracker.
- [x] **Phase 5: Story Engine:** Cross-source clustering and new information delta detector.
- [x] **Phase 6: YouTube Ingestion:** YouTube channel feeds, transcript extraction, and caption analyzer.
- [x] **Phase 7: Google Sheets Gateway:** Resilient offline queue, Apps Script bridge, and 2-tab sync (Opportunities & Tools).
- [x] **Phase 8: Autonomous Scheduling:** 24/7 background worker with 30-minute automated discovery passes.
- [x] **Phase 9: Multi-Language Translation:** 4-tier free translation engine with 100% strict English-only guarantee.
- [x] **Phase 10: Video Legitimacy & GitHub Extractor:** Scam detection for YouTube & Instagram Reels with extracted GitHub repository links.

---

## 11. Contributing & License

Contributions are welcome! Please submit PRs against the `develop` branch.

Licensed under the [MIT License](LICENSE).
