# SIGNAL — System Architecture

> **Tagline:** Your personal information firewall. Signal over noise.

---

## 1. Architectural Philosophy

Signal is designed as a **Modular Monolith** in .NET 10, optimizing strictly for **Signal > Noise** rather than engagement, page views, or notification volume. The system minimizes cloud operational cost ($0/month target) by processing high-volume feeds through lightweight deterministic filters before invoking LLMs or external search APIs.

```text
Sources (RSS, Official Blogs, YouTube, Reddit, Social)
                          ↓
              [ Ingestion Engine ]
                          ↓
              [ Normalization & Canonicalization ]
                          ↓
  [ Deduplication (URL, Content Hash, Title Similarity) ]
                          ↓
       [ Topic & Keyword Rule Pre-Filtering ]
                          ↓
            [ Story Clustering Engine ]
                          ↓
       [ Claim Extraction & Verification Engine ]
                          ↓
             [ Relevance & Scoring Engine ]
                          ↓
        [ Opportunity & Expiry Evaluation ]
                          ↓
              [ Telegram Interface ]
                          ↓
       [ User Review & Approval Gateway ]
                          ↓
   [ Personal Opportunity Tracker (Google Sheets) ]
```

---

## 2. Project Layout (.NET 10 Modular Monolith)

The codebase strictly adheres to Clean Architecture boundaries:

```text
signal/
├── src/
│   ├── Signal.Domain/            # Pure entities, domain events, value objects, core enums
│   ├── Signal.Application/       # Use cases, interfaces (providers, repositories), business rules
│   ├── Signal.Infrastructure/    # EF Core, SQLite/PostgreSQL, RSS parsers, HTTP clients, AI adapters
│   ├── Signal.Api/               # ASP.NET Core Minimal API endpoints for inspection & iOS app
│   └── Signal.Worker/            # Background scheduled ingestion, verification, & digest dispatcher
├── tests/
│   ├── Signal.UnitTests/         # In-memory unit tests for rules, scoring, deduplication
│   └── Signal.IntegrationTests/  # End-to-end integration tests for EF Core & pipeline flows
├── deploy/
│   ├── docker/                   # Container definitions & compose configs
│   └── cloudflare/               # Tunnel & edge routing configurations
├── docs/                         # Technical specifications and workflows
└── scripts/                      # Setup, migration, and automation scripts
```

---

## 3. Pipeline Stages & Computational Hierarchy

To guarantee zero-cost operation and low latency, compute is staged hierarchically:

1. **Ingestion & Normalization:**
   - Fetch XML/JSON/HTML feeds using resilient `HttpClient` policies (jittered exponential backoff).
   - Strip tracking parameters (`utm_*`, `ref`, `fbclid`, `gclid`).
   - Compute SHA-256 hash of normalized body content.
2. **Deterministic Deduplication (Zero AI):**
   - Exact URL match against `ContentItems`.
   - Canonical URL match.
   - Content SHA-256 hash match.
   - Normalized title Levenshtein & Jaccard token overlap (> 0.85).
3. **Cheap Rule-Based Filtering (Zero AI):**
   - Negative keyword rejection (clickbait tropes, engagement traps like "COMMENT AI", "DM ME").
   - Source Trust Tier weighting (Tiers 1 to 5).
4. **Story Clustering:**
   - Group related content items across multiple platforms (e.g. YouTube video + Blog post + Reddit discussion) into a single cohesive `Story`.
   - Track relationship types: `PRIMARY`, `COVERAGE`, `COMMENTARY`, `REPOST`, `DUPLICATE`.
5. **AI Analysis (Invoked Sparingly):**
   - `IAIProvider` interface with zero-cost fallback (Ollama local, Groq free tier, Gemini free tier, Mock).
   - Extract structured factual claims, products, and opportunity parameters.
6. **Multi-Factor Signal Scoring:**
   - Compute independent scores: Source Quality (0.20), Evidence (0.20), Relevance (0.25), Freshness (0.15), Opportunity (0.15), Urgency (0.05).
   - Apply penalties for ragebait patterns and duplicate coverage.
7. **Telegram Delivery:**
   - Send concise summaries with interactive inline buttons (`[Approve & Save]`, `[Save for Later]`, `[Reject]`, `[Show Evidence]`).
8. **Authoritative Approval to Google Sheets:**
   - Only explicitly approved items write to Google Sheets via Google Apps Script Web App.
   - Offline resilience: failed writes queue in `GoogleSheetRecord` with retry status.
