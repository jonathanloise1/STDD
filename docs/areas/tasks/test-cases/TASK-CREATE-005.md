# TASK-CREATE-005: Saving after the role became Viewer shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/TaskCreate005Tests, integration/TasksOutcomeCodesTests |

## Description

An editor whose role is changed to Viewer while the New Task form is open cannot save the task and is told why.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Prepare the quarterly report" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Prepare the quarterly report" in the "Title" field.
4. As Admin: Click the "Team" link in the menu.
5. As Admin: Click the "Edit" button on the row of "Dev Member".
6. As Admin: Check the "Viewer" field.
7. As Admin: Click the "Save" button.
8. Click the "Create Task" button.
9. As Admin: Click the "Edit" button on the row of "Dev Member".
10. As Admin: Check the "Editor" field.
11. As Admin: Click the "Save" button.
12. As Admin: Click the "Tasks" link in the menu.

## Expected Results

- After step 8, the message "Your role does not allow this change." appears.
- As Admin: the Tasks page does not list "Prepare the quarterly report".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "Your role does not allow this change." |
| Log key | `task.create.not_allowed` |
| Severity | Warning |
| Runbook | [task-create-not-allowed](../operations.md#task-create-not-allowed) |
