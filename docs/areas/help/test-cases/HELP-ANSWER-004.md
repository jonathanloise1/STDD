# HELP-ANSWER-004: A declined answer shows the closest sections

| Field | Value |
|-------|-------|
| User Story | US-HELP-05 |
| Type | Edge case |
| Priority | High |
| Automated by | e2e/HelpAnswer004Tests |

## Description

When the assistant declines to answer, the panel shows a fixed message and the closest sections.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The assistant is available on the server and declines to answer "How do I create a task".

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
- The section cards are shown below the message.
- The sentence the assistant used to decline is not shown.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The assistant could not answer this from the manual. These sections are the closest:" |
| Log key | `help.answer.declined` |
| Severity | Information |
| Runbook | [help-answer-declined](../operations.md#help-answer-declined) |
