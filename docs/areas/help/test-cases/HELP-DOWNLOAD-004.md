# HELP-DOWNLOAD-004: The downloaded entries show no person and no organization

| Field | Value |
|-------|-------|
| User Story | US-HELP-07 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/HelpDownload004Tests |

## Description

An entry of the file does not say who asked the question or for which organization.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".
- The manual says nothing about booking rooms.

## Test Data

| Field | Value |
|-------|-------|
| Your question | "can i book a meeting room from here" |
| From | "2020-01-01" |
| To | "2099-12-31" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "can i book a meeting room from here" in the "Your question" field.
5. Click the "Ask" button.
6. As Admin: Open the Unanswered questions page.
7. As Admin: Type "2020-01-01" in the "From" field.
8. As Admin: Type "2099-12-31" in the "To" field.
9. As Admin: Click the "Download" button.

## Expected Results

- As Admin: the downloaded file has an entry with the question "can i book a meeting room from here".
- As Admin: the file does not contain "Dev Member" or "MyApp Dev Org".
