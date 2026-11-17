# HELP-ASK-005: A question shows at most three cards

| Field | Value |
|-------|-------|
| User Story | US-HELP-04 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/HelpAsk005Tests |

## Description

A broad question shows three cards, never more.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- More than three sections of the manual contain the word "task".

## Test Data

| Field | Value |
|-------|-------|
| Your question | "task" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "task" in the "Your question" field.
5. Click the "Ask" button.

## Expected Results

- Exactly three cards are shown.
