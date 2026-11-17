# HELP-RATE-003: A yes is not passed on to the administrators

| Field | Value |
|-------|-------|
| User Story | US-HELP-06 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/HelpRate003Tests |

## Description

A question rated Yes does not appear in the download of the Admin.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".
- No assistant writes answers.

## Test Data

| Field | Value |
|-------|-------|
| Your question | "How do I delete a task that I created" |
| From | "2020-01-01" |
| To | "2099-12-31" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "How do I delete a task that I created" in the "Your question" field.
5. Click the "Ask" button.
6. Click the "Yes" button.
7. As Admin: Open the Unanswered questions page.
8. As Admin: Type "2020-01-01" in the "From" field.
9. As Admin: Type "2099-12-31" in the "To" field.
10. As Admin: Click the "Download" button.

## Expected Results

- As Admin: the downloaded file has no entry with the question "How do I delete a task that I created".
