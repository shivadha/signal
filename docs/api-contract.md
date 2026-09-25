# SIGNAL — RESTful API Contract

Base URL: `/api`  
Transport: HTTP/JSON  
Auth: Optional API Key header (`X-Signal-Key`) when configured for public network exposure.

---

## 1. Feed & Ingestion Endpoints

### `GET /api/feed`
Retrieves prioritized signals (Stories) filtered for high signal-to-noise ratio.
- **Query Parameters:**
  - `limit` (int, default 20)
  - `category` (string, optional: `AI`, `Tools`, `Developer`, `Offers`)
  - `minScore` (double, default 0.65)
- **Response `200 OK`:**
  ```json
  [
    {
      "id": "e9b5f137-083e-4d43-85f0-61f22adce2b1",
      "canonicalTopic": "OpenAI Swarm Agent Framework",
      "title": "OpenAI open-sources Swarm multi-agent orchestrator",
      "summary": "Educational framework demonstrating multi-agent ergonomic coordination.",
      "category": "AI Agents",
      "finalScore": 0.88,
      "verificationStatus": "Verified",
      "firstDetectedAt": "2026-09-25T08:00:00Z",
      "sourcesCount": 14,
      "primarySourceUrl": "https://github.com/openai/swarm"
    }
  ]
  ```

### `GET /api/offers`
Lists verified active and expiring opportunities.
- **Query Parameters:**
  - `status` (string, optional: `Active`, `ExpiringSoon`, `Approved`, `All`)
  - `isFree` (bool, default true)
- **Response `200 OK`:**
  ```json
  [
    {
      "id": "84c98f86-2182-4416-83ff-d98ec156372d",
      "title": "Anthropic Claude 3.5 Sonnet Tier Credits",
      "opportunityType": "FreeApiCredits",
      "value": "$25",
      "currency": "USD",
      "isFree": true,
      "eligibility": "New developer accounts",
      "expiryDate": "2026-10-31T23:59:59Z",
      "urgencyScore": 0.75,
      "verificationStatus": "Verified",
      "officialSourceUrl": "https://anthropic.com/pricing"
    }
  ]
  ```

### `GET /api/videos`
Retrieves curated, high-information-density videos (clustered and deduplicated).
- **Response `200 OK`:**
  ```json
  [
    {
      "id": "22477382-7632-4d0d-9b51-cfbc9deca81a",
      "title": "Deep dive into .NET 10 AI Abstractions",
      "channelName": "dotnet",
      "duration": "00:14:22",
      "watchScore": 0.92,
      "informationDensityScore": 0.89,
      "thumbnailUrl": "https://img.youtube.com/vi/abc123/hqdefault.jpg",
      "videoUrl": "https://www.youtube.com/watch?v=abc123"
    }
  ]
  ```

### `GET /api/stories/{id}`
Returns full story details, including all linked cross-platform coverage items.

### `GET /api/saved`
Returns items marked as approved or saved by the user.

### `GET /api/preferences`
Retrieves current topic and source affinity weights.

---

## 2. Interactive & Operational Endpoints

### `POST /api/check`
Submits any URL (YouTube, Reel, TikTok, Article) for instant deduplication, claim verification, and freshness evaluation.
- **Request Body:**
  ```json
  {
    "url": "https://youtube.com/watch?v=xyz789"
  }
  ```
- **Response `200 OK`:**
  ```json
  {
    "url": "https://youtube.com/watch?v=xyz789",
    "canonicalUrl": "https://youtube.com/watch?v=xyz789",
    "isKnownStory": true,
    "firstSeen": "2026-09-22T10:00:00Z",
    "hasNewInformation": false,
    "topic": "Claude Artifacts Export",
    "verificationStatus": "Verified",
    "relevanceScore": 0.90,
    "recommendation": "Duplicate commentary. No notification required."
  }
  ```

### `POST /api/feedback`
Records user telemetry from Telegram or mobile interface.
- **Request Body:**
  ```json
  {
    "contentItemId": "e9b5f137-083e-4d43-85f0-61f22adce2b1",
    "feedbackType": "Useful"
  }
  ```

### `POST /api/save`
Triggered when the user approves an opportunity. Enqueues a sync record to Google Sheets.
- **Request Body:**
  ```json
  {
    "opportunityId": "84c98f86-2182-4416-83ff-d98ec156372d",
    "notes": "Claimed on developer portal"
  }
  ```
- **Response `200 OK`:**
  ```json
  {
    "status": "Queued",
    "syncRecordId": "b1fa8514-416d-4951-b845-a7018804969b"
  }
  ```
