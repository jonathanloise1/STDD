# TASK-EDIT-004: Updating a task deleted by someone else shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-02 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/TaskEdit004Tests, integration/TasksOutcomeCodesTests |

## Description

An editor who updates a task that another member has just deleted is told that the task no longer exists, and the list is brought up to date.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Plan the offsite" |
| Title (new) | "Plan the team offsite" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Plan the offsite" in the "Title" field.
4. Click the "Create Task" button.
5. Click the "Edit" button on the row of "Plan the offsite".
6. Clear the "Title" field.
7. Type "Plan the team offsite" in the "Title" field.
8. As Admin: Open the Tasks page.
9. As Admin: Click the "Delete" button on the row of "Plan the offsite".
10. As Admin: Click the "Delete" button in the dialog.
11. Click the "Update" button.

## Expected Results

- The message "This task no longer exists." appears.
- The list shows neither "Plan the offsite" nor "Plan the team offsite".
- As Admin: the Tasks page does not list "Plan the offsite" or "Plan the team offsite".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "This task no longer exists." |
| Log key | `task.edit.not_found` |
| Severity | Warning |
| Runbook | [task-edit-not-found](../operations.md#task-edit-not-found) |
