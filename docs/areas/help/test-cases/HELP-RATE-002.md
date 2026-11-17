# HELP-RATE-002: A no reaches the administrators without a name

| Field | Value |
|-------|-------|
| User Story | US-HELP-06 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/HelpRate002Tests |

## Description

A question rated No appears in the download of the Admin, with no name of the person.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".
- No assistant writes answers.

## Test Data

| Field | Value |
|-------|-------|
| Your question | "How do I create a task before the deadline" |
| From | "2020-01-01" |
| To | "2099-12-31" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "How do I create a task before the deadline" in the "Your question" field.
5. Click the "Ask" button.
6. Click the "No" button.
7. As Admin: Open the Unanswered questions page.
8. As Admin: Type "2020-01-01" in the "From" field.
9. As Admin: Type "2099-12-31" in the "To" field.
10. As Admin: Click the "Download" button.

## Expected Results

- The message "Thanks for your feedback." appears.
- As Admin: the downloaded file has an entry with the question "How do I create a task before the deadline" and the reason "not_helpful".
- As Admin: the entry shows no name of a person.
