# TASK-EDIT-001: Edit the title and due date of a task

| Field | Value |
|-------|-------|
| User Story | US-TASK-02 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/TaskEdit001Tests |

## Description

An editor changes the title and the due date of a task and sees the new values in its row; the description is kept.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Draft the budget" |
| Description | "Use last year's figures" |
| Title (new) | "Draft the yearly budget" |
| Due Date | "2030-02-01" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Draft the budget" in the "Title" field.
4. Type "Use last year's figures" in the "Description" field.
5. Click the "Create Task" button.
6. Click the "Edit" button on the row of "Draft the budget".
7. Clear the "Title" field.
8. Type "Draft the yearly budget" in the "Title" field.
9. Type "2030-02-01" in the "Due Date" field.
10. Click the "Update" button.

## Expected Results

- The form closes.
- The list shows a row "Draft the yearly budget" with the due date "2030-02-01" and the description "Use last year's figures".
- The list does not show "Draft the budget".
