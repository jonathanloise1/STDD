# TASK-DELETE-004: Cancelling the confirmation keeps the task

| Field | Value |
|-------|-------|
| User Story | US-TASK-04 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/TaskDelete004Tests |

## Description

Clicking "Cancel" in the "Delete Task" dialog closes it and the task stays in the list.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Tasks page.
2. Click the "Delete" button on the row of "Add audit logging".
3. Click the "Cancel" button in the dialog.

## Expected Results

- The dialog is closed.
- The list still shows "Add audit logging".
