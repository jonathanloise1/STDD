# HELP-DOWNLOAD-002: A period ending before it starts shows a field message

| Field | Value |
|-------|-------|
| User Story | US-HELP-07 |
| Type | Validation |
| Priority | Medium |
| Automated by | e2e/HelpDownload002Tests |

## Description

The Download is refused when the To date is before the From date.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| From | "2030-02-01" |
| To | "2030-01-01" |

## Steps

1. Open the Unanswered questions page.
2. Type "2030-02-01" in the "From" field.
3. Type "2030-01-01" in the "To" field.
4. Click the "Download" button.

## Expected Results

- The "To" field shows "The end date cannot be before the start date."
- No file is downloaded.
