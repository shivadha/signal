/**
 * Google Apps Script Web App for Signal Platform
 * Deploy as Web App with access set to "Anyone"
 * Supports multiple tabs:
 * - "Opportunities" (Free developer credits, grants, tiers)
 * - "Tools" (GitHub repositories, productivity tools, developer utilities)
 */
function doPost(e) {
  var lock = LockService.getScriptLock();
  try {
    lock.waitLock(10000); // Wait up to 10 seconds for concurrent write safety
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var payload = JSON.parse(e.postData.contents);
    
    var isTool = payload.isRepo || payload.type === "tool" || payload.sheetName === "Tools";
    var targetSheetName = isTool ? "Tools" : "Opportunities";
    
    var sheet = ss.getSheetByName(targetSheetName);
    if (!sheet) {
      sheet = ss.insertSheet(targetSheetName);
      if (isTool) {
        // Headers for Tools / GitHub Repositories
        sheet.appendRow([
          "Timestamp",
          "Tool / Repo Name",
          "Category",
          "Repository URL",
          "Description",
          "Signal Score",
          "Status",
          "Notes"
        ]);
        sheet.getRange(1, 1, 1, 8).setFontWeight("bold").setBackground("#e8f0fe");
      } else {
        // Headers for Opportunities
        sheet.appendRow([
          "Timestamp",
          "Category",
          "Title",
          "Description",
          "Canonical URL",
          "Official Source URL",
          "Opportunity Type",
          "Value",
          "Currency",
          "Eligibility",
          "Expiry Date",
          "Verification Status",
          "Relevance Score",
          "Status",
          "Why Useful",
          "Notes"
        ]);
        sheet.getRange(1, 1, 1, 16).setFontWeight("bold").setBackground("#e6f4ea");
      }
    }
    
    var data = sheet.getDataRange().getValues();
    var urlToCheck = payload.canonicalUrl || payload.url || "";
    var urlColIdx = isTool ? 3 : 4; // Col D (index 3) for Tools, Col E (index 4) for Opportunities
    
    // Check if URL already exists
    for (var i = 1; i < data.length; i++) {
      if (data[i][urlColIdx] && urlToCheck && data[i][urlColIdx].toString().trim() === urlToCheck.toString().trim()) {
        sheet.getRange(i + 1, isTool ? 7 : 14).setValue("Approved (Updated)");
        return ContentService.createTextOutput(JSON.stringify({
          status: "updated",
          sheet: targetSheetName,
          row: i + 1,
          message: "Existing row updated in " + targetSheetName
        })).setMimeType(ContentService.MimeType.JSON);
      }
    }
    
    // Append row according to sheet type
    if (isTool) {
      sheet.appendRow([
        new Date().toISOString(),
        payload.title || "GitHub Tool",
        payload.category || "Developer Tools",
        payload.url || payload.canonicalUrl || "",
        payload.description || "",
        payload.relevanceScore || 1.0,
        payload.status || "Saved",
        payload.notes || "Saved via Signal Bot /tools"
      ]);
    } else {
      sheet.appendRow([
        new Date().toISOString(),
        payload.category || "AI",
        payload.title || "",
        payload.description || "",
        payload.canonicalUrl || payload.url || "",
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
      ]);
    }
    
    var rowIdx = sheet.getLastRow();
    
    return ContentService.createTextOutput(JSON.stringify({
      status: "success",
      sheet: targetSheetName,
      row: rowIdx,
      message: "Row appended successfully to " + targetSheetName
    })).setMimeType(ContentService.MimeType.JSON);
  } catch (err) {
    return ContentService.createTextOutput(JSON.stringify({
      status: "error",
      message: err.toString()
    })).setMimeType(ContentService.MimeType.JSON);
  } finally {
    lock.releaseLock();
  }
}
