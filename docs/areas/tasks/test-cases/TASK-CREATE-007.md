# TASK-CREATE-007: A viewer cannot create tasks

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Security |
| Priority | High |
| Automated by | e2e/TaskCreate007Tests |

## Description

A viewer sees the tasks of the organization but is offered no way to add one.

## Preconditions

- Signed in as Viewer of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The list shows "Set up CI/CD pipeline".
- No "New Task" button is shown.
