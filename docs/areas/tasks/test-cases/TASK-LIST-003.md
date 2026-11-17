# TASK-LIST-003: A missing due date is shown as a dash

| Field | Value |
|-------|-------|
| User Story | US-TASK-05 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/TaskList003Tests |

## Description

A task without a due date shows "-" in the Due Date column; a viewer's table has no Actions column.

## Preconditions

- Signed in as Viewer of "MyApp Dev Org".
- The organization has the demo tasks described in the test suite.

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The row of "Set up monitoring dashboard" shows "-" under "Due Date".
- The table has the columns "Title", "Status" and "Due Date" and no column "Actions".
