# HELP-ASK-004: The matches come from screens other than the current one

| Field | Value |
|-------|-------|
| User Story | US-HELP-04 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/HelpAsk004Tests |

## Description

On the Dashboard page, a question about deleting a task shows the page about deleting a task.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Your question | "How do I delete a task" |

## Steps

1. Open the Dashboard page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "How do I delete a task" in the "Your question" field.
5. Click the "Ask" button.

## Expected Results

- A card shows the page title "Delete a task".
