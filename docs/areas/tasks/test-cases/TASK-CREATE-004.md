# TASK-CREATE-004: A title made only of spaces shows a field message

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Validation |
| Priority | Medium |
| Automated by | e2e/TaskCreate004Tests |

## Description

A title made only of spaces counts as empty: the form stays open, the field says why and no task is added.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "   " |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "   " in the "Title" field.
4. Click the "Create Task" button.

## Expected Results

- The form stays open.
- The "Title" field shows "Enter a title."
- The list does not show a new row with an empty title.
