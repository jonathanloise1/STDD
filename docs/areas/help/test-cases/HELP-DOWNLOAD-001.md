# HELP-DOWNLOAD-001: An Admin downloads a question the manual could not answer

| Field | Value |
|-------|-------|
| User Story | US-HELP-07 |
| Type | Happy path |
| Priority | Medium |
| Automated by | e2e/HelpDownload001Tests |

## Description

A question the manual cannot answer is in the file the Admin downloads for a period that includes it.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".
- The manual says nothing about emails on assignment.

## Test Data

| Field | Value |
|-------|-------|
| Your question | "can i get an email when someone assigns me a task" |
| From | "2020-01-01" |
| To | "2099-12-31" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "can i get an email when someone assigns me a task" in the "Your question" field.
5. Click the "Ask" button.
6. As Admin: Open the Unanswered questions page.
7. As Admin: Type "2020-01-01" in the "From" field.
8. As Admin: Type "2099-12-31" in the "To" field.
9. As Admin: Click the "Download" button.

## Expected Results

- As Admin: the downloaded file has an entry with the question "can i get an email when someone assigns me a task".
- As Admin: the entry shows the language "en" and the reason "no_match".
- As Admin: the entry shows the date and time and the screen where the question was asked.
