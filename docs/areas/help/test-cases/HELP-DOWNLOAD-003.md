# HELP-DOWNLOAD-003: E-mail addresses, web addresses and numbers are replaced

| Field | Value |
|-------|-------|
| User Story | US-HELP-07 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/HelpDownload003Tests |

## Description

Personal details typed in a question are replaced in the downloaded file.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".
- The manual says nothing about paying invoices.

## Test Data

| Field | Value |
|-------|-------|
| Your question | "can i pay invoice 12345678 from here, write to anna.rossi@example.com or see https://example.com/pay" |
| From | "2020-01-01" |
| To | "2099-12-31" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "can i pay invoice 12345678 from here, write to anna.rossi@example.com or see https://example.com/pay" in the "Your question" field.
5. Click the "Ask" button.
6. As Admin: Open the Unanswered questions page.
7. As Admin: Type "2020-01-01" in the "From" field.
8. As Admin: Type "2099-12-31" in the "To" field.
9. As Admin: Click the "Download" button.

## Expected Results

- As Admin: the downloaded file has an entry with the question "can i pay invoice [number] from here, write to [email] or see [url]".
- As Admin: the file does not contain "12345678", "anna.rossi@example.com" or "https://example.com/pay".
