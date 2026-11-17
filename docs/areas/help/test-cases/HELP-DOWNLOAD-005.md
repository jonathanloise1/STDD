# HELP-DOWNLOAD-005: A period with no questions gives a file with no entries

| Field | Value |
|-------|-------|
| User Story | US-HELP-07 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/HelpDownload005Tests |

## Description

An Admin who downloads a period without unanswered questions gets a file with nothing in it.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- No question was asked in January 2001.

## Test Data

| Field | Value |
|-------|-------|
| From | "2001-01-01" |
| To | "2001-01-31" |

## Steps

1. Open the Unanswered questions page.
2. Type "2001-01-01" in the "From" field.
3. Type "2001-01-31" in the "To" field.
4. Click the "Download" button.

## Expected Results

- A file is downloaded.
- The file has no entries.
