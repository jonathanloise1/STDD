# HELP-ASK-008: Asking shows a message when the help cannot be loaded

| Field | Value |
|-------|-------|
| User Story | US-HELP-04 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/HelpAsk008Tests |

## Description

When the help content cannot be loaded, asking a question says help is unavailable.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The help content cannot be loaded.

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

## Expected Results

- The message "Help is unavailable." appears.
- No card is shown.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "Help is unavailable." |
| Log key | `help.ask.unavailable` |
| Severity | Error |
| Runbook | [help-ask-unavailable](../operations.md#help-ask-unavailable) |
