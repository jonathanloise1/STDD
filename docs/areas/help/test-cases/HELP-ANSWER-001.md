# HELP-ANSWER-001: An answer shows its source and where it was written

| Field | Value |
|-------|-------|
| User Story | US-HELP-05 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/HelpAnswer001Tests |

## Description

A short answer appears above the section cards, with a source link and a footer line.

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

## Expected Results

- A card labelled "Answer from the manual" appears above the section cards.
- The answer shows one source link, labelled with the number of the section it cites.
- The footer line reads "Answered by the server".
- The first section card is the cited one, from the page "Create a task".
