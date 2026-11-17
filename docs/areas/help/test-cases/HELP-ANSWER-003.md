# HELP-ANSWER-003: An answer without a source is never shown

| Field | Value |
|-------|-------|
| User Story | US-HELP-05 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/HelpAnswer003Tests |

## Description

When the assistant gives an answer with no source, the panel shows the closest sections instead.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The assistant is available on the server and answers "How do I create a task" with no source.

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

- The message "The assistant could not answer this from the manual. These sections are the closest:" appears.
- No card is labelled "Answer from the manual".
- The section cards are shown.
