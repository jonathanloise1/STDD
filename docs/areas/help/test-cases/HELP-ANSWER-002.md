# HELP-ANSWER-002: A source link opens the cited section

| Field | Value |
|-------|-------|
| User Story | US-HELP-05 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/HelpAnswer002Tests |

## Description

The source link of an answer opens the cited section in the manual.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The assistant is available on the server and answers "How do I create a task" citing a section of the page "Create a task" as its source.

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
6. Click the source link of the answer.

## Expected Results

- The Manual page shows the page "Create a task" scrolled to the cited section.
