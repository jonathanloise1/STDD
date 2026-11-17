# TASK-LIST-004: Loading when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-05 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/TaskList004Tests, integration/TasksOutcomeCodesTests |

## Description

When the service does not answer while the tasks are loaded, the page says so instead of showing an empty list.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The service does not answer when the tasks are loaded.

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The message "The service did not answer. Try again in a few minutes." appears.
- The page does not show "No tasks yet".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `task.list.unavailable` |
| Severity | Error |
| Runbook | [task-list-unavailable](../operations.md#task-list-unavailable) |
