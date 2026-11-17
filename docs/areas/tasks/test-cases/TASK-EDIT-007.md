# TASK-EDIT-007: Updating after the role became Viewer shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-02 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/TaskEdit007Tests, integration/TasksOutcomeCodesTests |

## Description

An editor whose role is changed to Viewer while the edit form is open cannot update the task and is told why; the task keeps its title.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".
- The Tasks page lists a task titled "Review security configuration".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Review the firewall rules" |

## Steps

1. Open the Tasks page.
2. Click the "Edit" button on the row of "Review security configuration".
3. Clear the "Title" field.
4. Type "Review the firewall rules" in the "Title" field.
5. As Admin: Click the "Team" link in the menu.
6. As Admin: Click the "Edit" button on the row of "Dev Member".
7. As Admin: Check the "Viewer" field.
8. As Admin: Click the "Save" button.
9. Click the "Update" button.
10. As Admin: Click the "Edit" button on the row of "Dev Member".
11. As Admin: Check the "Editor" field.
12. As Admin: Click the "Save" button.
13. As Admin: Click the "Tasks" link in the menu.

## Expected Results

- After step 9, the message "Your role does not allow this change." appears.
- As Admin: the Tasks page lists "Review security configuration" and does not list "Review the firewall rules".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "Your role does not allow this change." |
| Log key | `task.edit.not_allowed` |
| Severity | Warning |
| Runbook | [task-edit-not-allowed](../operations.md#task-edit-not-allowed) |
