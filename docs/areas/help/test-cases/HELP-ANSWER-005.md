# HELP-ANSWER-005: A slow answer shows the best matches instead

| Field | Value |
|-------|-------|
| User Story | US-HELP-05 |
| Type | Edge case |
| Priority | High |
| Automated by | e2e/HelpAnswer005Tests |

## Description

When the only assistant takes too long, the panel shows the best matching sections.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The assistant is available on the server and takes too long to answer "How do I create a task".

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

- The header "Best matches in the manual" appears above the section cards.
- No card is labelled "Answer from the manual".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "Best matches in the manual" |
| Log key | `help.answer.timeout` |
| Severity | Warning |
| Runbook | [help-answer-timeout](../operations.md#help-answer-timeout) |
