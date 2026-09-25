/**
 * Google Apps Script Web App for Signal Platform
 * Deploy as Web App with access set to "Anyone"
 */
function doPost(e) {
  var lock = LockService.getScriptLock();
  try {
    lock.waitLock(10000); // Wait up to 10 seconds for concurrent write safety
    var sheet = SpreadsheetApp.getActiveSpreadsheet().getActiveSheet();
    var payload = JSON.parse(e.postData.contents);
    
    // Check if canonical URL already exists in Column E (index 5)
    var data = sheet.getDataRange().getValues();
    for (var i = 1; i < data.length; i++) {
      if (data[i][4] && payload.canonicalUrl && data[i][4].toString().trim() === payload.canonicalUrl.toString().trim()) {
        // Update existing row
        sheet.getRange(i + 1, 12).setValue(payload.verificationStatus || "Verified");
        sheet.getRange(i + 1, 14).setValue("Approved (Updated)");
        return ContentService.createTextOutput(JSON.stringify({
          status: "updated",
          row: i + 1,
          message: "Existing row updated"
        })).setMimeType(ContentService.MimeType.JSON);
      }
    }
    
    // Append new row
    var newRow = [
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
    ];
    
    sheet.appendRow(newRow);
    var rowIdx = sheet.getLastRow();
    
    return ContentService.createTextOutput(JSON.stringify({
      status: "success",
      row: rowIdx,
      message: "Row appended successfully"
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
