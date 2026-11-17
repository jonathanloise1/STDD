# TASK-LIST-001: The Tasks page lists the tasks of the organization

| Field | Value |
|-------|-------|
| User Story | US-TASK-05 |
| Type | Happy path |
| Priority | Critical |
| Automated by | e2e/TaskList001Tests |

## Description

A signed-in admin opens the Tasks page from the menu and sees the tasks of the organization in a table.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The organization has the demo tasks described in the test suite.

## Test Data

None.

## Steps

1. Click the "Tasks" link in the menu.

## Expected Results

- The Tasks page opens.
- The table has the columns "Title", "Status", "Due Date" and "Actions".
- The row of "Create task management UI" shows the status "In Progress".
- The row of "Set up CI/CD pipeline" shows the status "Done".
