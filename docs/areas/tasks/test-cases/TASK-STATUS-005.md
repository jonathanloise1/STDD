# TASK-STATUS-005: A viewer cannot change the status of a task

| Field | Value |
|-------|-------|
| User Story | US-TASK-03 |
| Type | Security |
| Priority | High |
| Automated by | e2e/TaskStatus005Tests |

## Description

A viewer sees the status of each task but no row offers a "Change status" button.

## Preconditions

- Signed in as Viewer of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The row of "Set up CI/CD pipeline" shows the status "Done".
- No row shows a "Change status" button.
