# SIGNAL — Google Sheets Approval & Persistence Workflow

The Google Sheets integration acts as the user's **authoritative personal opportunity archive**. Signal strictly isolates this repository from internal application state.

---

## 1. Zero-Cost Apps Script Bridge

To avoid complex GCP service account key setups and OAuth consent screen renewal on personal machines, Signal defaults to an HTTPS Webhook via a **Google Apps Script Web App**:

```text
[ Telegram User Tap: "APPROVE & SAVE" ]
                     ↓
        Signal.Api / Signal.Worker
                     ↓
         [ Verification Check ]
                     ↓
  [ GoogleSheetApprovalService ]
                     ↓
   POST JSON -> Google Apps Script Endpoint
                     ↓
    Append / Update Row in Google Sheet
                     ↓
      Return Row Index (e.g. 142)
                     ↓
   Signal Telegram Bot: "✅ SAVED #142"
```

---

## 2. Failure Resilience & Offline Queue

If the Google Apps Script endpoint is unreachable, timed out, or rate-limited:
1. Signal **never discards** approved items.
2. An entry is saved into `GoogleSheetRecord` in SQLite with `Status = PendingSync`.
3. The background worker polls for pending records every 5 minutes and executes exponential backoff retries.
4. Once the sync succeeds, the record is transitioned to `Status = Synced` and an updated notification confirms synchronization to the user.

---

## 3. Deduplication Before Insertion

Before creating a new row in Google Sheets, the integration performs idempotency checks:
- Match `CanonicalUrl`
- Match `OpportunityId`
- Match `StoryId`

If the item already exists in the target spreadsheet:
- If new information is present (e.g. extended expiry, new promo code), update the existing row.
- If the item is identical, return the existing row number without generating duplicate rows.

---

## 4. Google Apps Script Web App Code (`deploy/google-apps-script.js`)

Users can paste this script into **Extensions > Apps Script** in their Google Sheet and deploy it as a Web App:

```javascript
function doPost(e) {
  try {
    var sheet = SpreadsheetApp.getActiveSpreadsheet().getActiveSheet();
    var payload = JSON.parse(e.postData.contents);
    
    // Check if canonical URL exists in column E (index 5)
    var data = sheet.getDataRange().getValues();
    for (var i = 1; i < data.length; i++) {
      if (data[i][4] === payload.canonicalUrl) {
        // Update existing row
        sheet.getRange(i + 1, 14).setValue(payload.verificationStatus);
        sheet.getRange(i + 1, 18).setValue(new Date());
        return ContentService.createTextOutput(JSON.stringify({ status: "updated", row: i + 1 }))
          .setMimeType(ContentService.MimeType.JSON);
      }
    }
    
    // Append new row
    var newRow = [
      new Date(),
      payload.category || "AI",
      payload.title || "",
      payload.description || "",
      payload.url || "",
      payload.officialSourceUrl || "",
      payload.opportunityType || "",
      payload.value || "",
      payload.currency || "USD",
      payload.eligibility || "",
      payload.expiryDate || "",
      payload.verificationStatus || "Verified",
      payload.relevanceScore || "",
      "Approved",
      payload.whyUseful || "",
      payload.notes || ""
    ];
    sheet.appendRow(newRow);
    var rowIdx = sheet.getLastRow();
    
    return ContentService.createTextOutput(JSON.stringify({ status: "success", row: rowIdx }))
      .setMimeType(ContentService.MimeType.JSON);
  } catch (err) {
    return ContentService.createTextOutput(JSON.stringify({ status: "error", message: err.toString() }))
      .setMimeType(ContentService.MimeType.JSON);
  }
}
```
