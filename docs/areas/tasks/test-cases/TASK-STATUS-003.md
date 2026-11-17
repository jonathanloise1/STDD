# TASK-STATUS-003: A cancelled task goes back to Todo

| Field | Value |
|-------|-------|
| User Story | US-TASK-03 |
| Type | Business rule |
| Priority | Low |
| Automated by | e2e/TaskStatus003Tests |

## Description

Clicking the "Change status" button of a cancelled task changes its status to "Todo".

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Replace the printer" |
| Status | "Cancelled" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Replace the printer" in the "Title" field.
4. Click the "Create Task" button.
5. Click the "Edit" button on the row of "Replace the printer".
6. Select "Cancelled" in the "Status" field.
7. Click the "Update" button.
8. Click the "Change status" button on the row of "Replace the printer".

## Expected Results

- The row of "Replace the printer" shows the status "Todo".
