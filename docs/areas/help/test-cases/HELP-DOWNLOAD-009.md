# HELP-DOWNLOAD-009: A failed download shows a message

| Field | Value |
|-------|-------|
| User Story | US-HELP-07 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/HelpDownload009Tests |

## Description

When the file cannot be prepared, the Admin is told and no file is downloaded.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The file with the questions cannot be prepared.

## Test Data

| Field | Value |
|-------|-------|
| From | "2020-01-01" |
| To | "2099-12-31" |

## Steps

1. Open the Unanswered questions page.
2. Type "2020-01-01" in the "From" field.
3. Type "2099-12-31" in the "To" field.
4. Click the "Download" button.

## Expected Results

- The message "The questions could not be downloaded." appears.
- No file is downloaded.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The questions could not be downloaded." |
| Log key | `help.download.failed` |
| Severity | Error |
| Runbook | [help-download-failed](../operations.md#help-download-failed) |
