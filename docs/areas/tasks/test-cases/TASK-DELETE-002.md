# TASK-DELETE-002: Deleting a task already deleted shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-04 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/TaskDelete002Tests, integration/TasksOutcomeCodesTests |

## Description

An editor who confirms the deletion of a task that another member has just deleted is told that the task no longer exists.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Clean the shared drive" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Clean the shared drive" in the "Title" field.
4. Click the "Create Task" button.
5. As Admin: Open the Tasks page.
6. As Admin: Click the "Delete" button on the row of "Clean the shared drive".
7. As Admin: Click the "Delete" button in the dialog.
8. Click the "Delete" button on the row of "Clean the shared drive".
9. Click the "Delete" button in the dialog.

## Expected Results

- The message "This task no longer exists." appears.
- The list does not show "Clean the shared drive".
- As Admin: the Tasks page does not list "Clean the shared drive".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "This task no longer exists." |
| Log key | `task.delete.not_found` |
| Severity | Warning |
| Runbook | [task-delete-not-found](../operations.md#task-delete-not-found) |
