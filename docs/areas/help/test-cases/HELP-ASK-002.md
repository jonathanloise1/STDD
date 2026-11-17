# HELP-ASK-002: The question field accepts at most 300 characters

| Field | Value |
|-------|-------|
| User Story | US-HELP-04 |
| Type | Validation |
| Priority | High |
| Automated by | e2e/HelpAsk002Tests |

## Description

Typing more than 300 characters keeps only the first 300 in the field.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Your question | "the letter a typed 301 times" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "the letter a typed 301 times" in the "Your question" field.

## Expected Results

- The "Your question" field shows exactly 300 characters.
