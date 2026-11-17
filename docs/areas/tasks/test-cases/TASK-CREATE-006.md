# TASK-CREATE-006: Cancelling closes the form without adding a task

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/TaskCreate006Tests |

## Description

Clicking "Cancel" closes the form and discards what was typed.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Draft that will be discarded" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Draft that will be discarded" in the "Title" field.
4. Click the "Cancel" button.

## Expected Results

- The form is closed.
- The list does not show "Draft that will be discarded".
