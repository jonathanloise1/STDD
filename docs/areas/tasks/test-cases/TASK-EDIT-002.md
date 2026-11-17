# TASK-EDIT-002: Updating with an empty title shows a field message

| Field | Value |
|-------|-------|
| User Story | US-TASK-02 |
| Type | Validation |
| Priority | High |
| Automated by | e2e/TaskEdit002Tests |

## Description

A task cannot be updated while the Title field is empty; the field says why and the task keeps its title.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The Tasks page lists a task titled "Add audit logging".

## Test Data

None.

## Steps

1. Open the Tasks page.
2. Click the "Edit" button on the row of "Add audit logging".
3. Clear the "Title" field.
4. Click the "Update" button.
5. Click the "Cancel" button.

## Expected Results

- After step 4, the form stays open and the "Title" field shows "Enter a title."
- After step 5, the list still shows "Add audit logging".
