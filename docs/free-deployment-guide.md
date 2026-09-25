# Free Deployment Guide for SIGNAL ($0/month, 24/7 Operation)

This guide walks you through deploying **SIGNAL** completely free of charge, with maximum security and 24/7 reliability.

---

## What SIGNAL Does in the Background (24/7)

1. **Automated 30-Minute Crawl & Intelligence Updates**:
   - Every 30 minutes, all active feeds (OpenAI, Anthropic, Google, Hugging Face, GitHub, MIT Tech, YouTube) are ingested and deduplicated.
   - If **developer opportunities** (free credits, vouchers, dev discounts) are detected, interactive cards with `[✅ APPROVE & SAVE]` buttons are immediately delivered to your Telegram chat.
   - If **new high-signal tech stories** are discovered, a formatted digest with titles, summaries, sources, and links is automatically pushed to your Telegram.
   - If no new items were published in the interval, **zero noise** is sent.

2. **Interactive Telegram Bot (Instant Response)**:
   - Responds in ~0.3s to commands:
     - `/today` (or `today`) — Full details, excerpts, sources, and links for top discoveries.
     - `/offers` (or `offers`) — Verified free developer credits and tiers.
     - `/saved` (or `saved`) — Your approved library.
     - `/check <url>` (or paste any URL) — Analyzes links for rage-bait, opportunities, and duplicates.

---

## Safety & Security Best Practices

> [!IMPORTANT]
> **Never commit your `.env` file or API tokens to GitHub.**
> - Your local `.env` is already listed in `.gitignore`.
> - In any cloud provider, secrets must be configured exclusively in the provider's **Environment Variables** dashboard.
> - The database (`signal.db`) is local to your container volume and never exposed publicly.

---

## Deployment Options

### Option 1: Render.com (Recommended — 100% Free, Auto-Deploy from GitHub)

Render allows you to deploy a Docker container directly connected to your GitHub repository for free.

#### Steps:
1. Go to [https://dashboard.render.com](https://dashboard.render.com) and sign in (using GitHub).
2. Click **New +** > **Web Service**.
3. Select your repository: **`shivadha/signal`**.
4. Configure the service settings:
   - **Name:** `signal-bot`
   - **Region:** Any (e.g., Oregon or Frankfurt)
   - **Branch:** `develop` (or `main`)
   - **Runtime:** `Docker`
   - **Instance Type:** `Free`
5. Scroll down to **Environment Variables** and add:
   | Key | Value |
   |---|---|
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `DATABASE_CONNECTION_STRING` | `Data Source=/app/data/signal.db` |
   | `AI_PROVIDER` | `none` |
   | `TELEGRAM_BOT_TOKEN` | *Your Telegram Bot Token* |
   | `TELEGRAM_CHAT_ID` | `5312511086` |
   | `ENABLE_WORKER` | `true` |
6. Click **Create Web Service**.
7. Render will build the Docker container and start your 24/7 service.
   - The `/health` endpoint keeps the service verified and healthy.
   - In the background, the bot answers Telegram commands instantly and pushes updates every 30 minutes.

---

### Option 2: Koyeb (100% Free Eco Nano Instance)

Koyeb offers a free Eco nano instance that runs Docker containers continuously 24/7.

1. Sign up at [https://app.koyeb.com](https://app.koyeb.com).
2. Click **Create Service** > **GitHub**.
3. Select `shivadha/signal` and branch `develop`.
4. Choose **Dockerfile** as build type.
5. In **Environment Variables**, add `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`, and `DATABASE_CONNECTION_STRING=Data Source=/app/data/signal.db`.
6. Select the **Eco Free (Nano)** instance type and click **Deploy**.

---

### Option 3: Fly.io (Free Tier with Persistent SQLite Storage)

Fly.io gives 3 free VMs and allows persistent volume storage for SQLite.

1. Install Fly CLI: `winget install flyctl` or `curl -L https://fly.io/install.sh | sh`.
2. Authenticate: `fly auth login`.
3. In `C:\AI_project\signal`:
   ```bash
   fly launch --no-deploy
   fly volumes create signal_data --size 1
   fly secrets set TELEGRAM_BOT_TOKEN="your_token" TELEGRAM_CHAT_ID="5312511086"
   fly deploy
   ```

---

### Option 4: Local Windows 24/7 Background Runner (Zero Cloud Setup)

If you keep your PC or a home server running, you can run SIGNAL locally with zero cloud dependencies:

1. **Run in Background Silently**:
   - Double-click [`deploy/start-silent.vbs`](file:///C:/AI_project/signal/deploy/start-silent.vbs).
   - This starts the worker invisibly in the background with zero visible terminal windows.
2. **Start Automatically on Windows Boot**:
   - Press `Win + R`, type `shell:startup`, and press Enter.
   - Create a shortcut to [`deploy/start-silent.vbs`](file:///C:/AI_project/signal/deploy/start-silent.vbs) in this Startup folder.
   - SIGNAL will now start automatically whenever your computer powers on.

---

## Verification Checklist

- [x] Send `/start` or `start` in Telegram: receives 24/7 status card.
- [x] Send `/today` or `today`: receives top stories with summaries, sources, and direct links.
- [x] Send `/offers` or `offers`: displays active developer free tiers & credits.
- [x] Paste any article or YouTube link: receives link verification and duplicate check.
- [x] Automated 30-minute background job: periodically checks all feeds and delivers new discoveries automatically.
