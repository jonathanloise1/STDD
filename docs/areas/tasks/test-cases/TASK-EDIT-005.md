# TASK-EDIT-005: Cancelling an edit keeps the previous values

| Field | Value |
|-------|-------|
| User Story | US-TASK-02 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/TaskEdit005Tests |

## Description

Clicking "Cancel" closes the edit form and the task keeps its title.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The Tasks page lists a task titled "Set up monitoring dashboard".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Changed title" |

## Steps

1. Open the Tasks page.
2. Click the "Edit" button on the row of "Set up monitoring dashboard".
3. Clear the "Title" field.
4. Type "Changed title" in the "Title" field.
5. Click the "Cancel" button.

## Expected Results

- The form is closed.
- The list shows "Set up monitoring dashboard" and does not show "Changed title".
