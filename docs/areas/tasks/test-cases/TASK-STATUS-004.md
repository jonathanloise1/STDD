# TASK-STATUS-004: Changing the status of a deleted task shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-03 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/TaskStatus004Tests, integration/TasksOutcomeCodesTests |

## Description

An editor who clicks the "Change status" button of a task that another member has just deleted is told that the task no longer exists.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Renew the licences" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Renew the licences" in the "Title" field.
4. Click the "Create Task" button.
5. As Admin: Open the Tasks page.
6. As Admin: Click the "Delete" button on the row of "Renew the licences".
7. As Admin: Click the "Delete" button in the dialog.
8. Click the "Change status" button on the row of "Renew the licences".

## Expected Results

- The message "This task no longer exists." appears.
- The list does not show "Renew the licences".
- As Admin: the Tasks page does not list "Renew the licences".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "This task no longer exists." |
| Log key | `task.status.not_found` |
| Severity | Warning |
| Runbook | [task-status-not-found](../operations.md#task-status-not-found) |
