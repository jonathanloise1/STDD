# TASK-DELETE-003: A viewer cannot delete tasks

| Field | Value |
|-------|-------|
| User Story | US-TASK-04 |
| Type | Security |
| Priority | High |
| Automated by | e2e/TaskDelete003Tests |

## Description

A viewer sees the tasks but no row offers a "Delete" button.

## Preconditions

- Signed in as Viewer of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The list shows "Review security configuration".
- No row shows a "Delete" button.
