# HELP-ASK-007: A visitor cannot ask a question

| Field | Value |
|-------|-------|
| User Story | US-HELP-04 |
| Type | Security |
| Priority | High |
| Automated by | e2e/HelpAsk007Tests |

## Description

A person who is not signed in cannot reach the Ask tab.

## Preconditions

- Not signed in.

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The Sign in page opens.
- The page shows no "Ask" tab and no "Your question" field.
