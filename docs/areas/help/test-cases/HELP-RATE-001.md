# HELP-RATE-001: Saying yes shows a thank-you message

| Field | Value |
|-------|-------|
| User Story | US-HELP-06 |
| Type | Happy path |
| Priority | Medium |
| Automated by | e2e/HelpRate001Tests |

## Description

After a result the panel asks whether it helped, and a click on Yes is acknowledged.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- No assistant writes answers.

## Test Data

| Field | Value |
|-------|-------|
| Your question | "How do I create a task" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "How do I create a task" in the "Your question" field.
5. Click the "Ask" button.
6. Click the "Yes" button.

## Expected Results

- Under the cards the panel asks "Did this help?" with the buttons "Yes" and "No".
- The message "Thanks for your feedback." appears.
- The cards are still shown.
