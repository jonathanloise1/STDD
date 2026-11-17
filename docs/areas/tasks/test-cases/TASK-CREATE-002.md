# TASK-CREATE-002: Saving without a title shows a field message

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Validation |
| Priority | High |
| Automated by | e2e/TaskCreate002Tests |

## Description

A task cannot be created while the Title field is empty; the form stays open and the field says why.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Description | "Task without a title" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Task without a title" in the "Description" field.
4. Click the "Create Task" button.

## Expected Results

- The form stays open.
- The "Title" field shows "Enter a title."
- The list does not show "Task without a title".
