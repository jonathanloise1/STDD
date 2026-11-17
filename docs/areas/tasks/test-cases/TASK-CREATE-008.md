# TASK-CREATE-008: Saving when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/TaskCreate008Tests, integration/TasksOutcomeCodesTests |

## Description

When the service does not answer while a task is saved, the editor is told so and keeps what was typed.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The service does not answer when a task is saved.

## Test Data

| Field | Value |
|-------|-------|
| Title | "Book the training room" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Book the training room" in the "Title" field.
4. Click the "Create Task" button.

## Expected Results

- The message "The service did not answer. Try again in a few minutes." appears.
- The form stays open and the "Title" field still shows "Book the training room".
- The list does not show "Book the training room".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `task.create.unavailable` |
| Severity | Error |
| Runbook | [task-create-unavailable](../operations.md#task-create-unavailable) |
