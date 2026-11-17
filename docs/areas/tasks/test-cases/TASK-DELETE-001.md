# TASK-DELETE-001: Delete a task after confirming

| Field | Value |
|-------|-------|
| User Story | US-TASK-04 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/TaskDelete001Tests |

## Description

The "Delete" button asks for confirmation; confirming removes the task from the list.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Remove the old banner" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Remove the old banner" in the "Title" field.
4. Click the "Create Task" button.
5. Click the "Delete" button on the row of "Remove the old banner".
6. Click the "Delete" button in the dialog.

## Expected Results

- After step 5, the "Delete Task" dialog asks "Are you sure you want to delete this task?"
- After step 6, the dialog is closed.
- The list does not show "Remove the old banner".
