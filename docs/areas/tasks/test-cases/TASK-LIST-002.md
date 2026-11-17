# TASK-LIST-002: A newly created task is at the top of the list

| Field | Value |
|-------|-------|
| User Story | US-TASK-05 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/TaskList002Tests |

## Description

The newest task is shown first, above older tasks.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Newest task on top" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Newest task on top" in the "Title" field.
4. Click the "Create Task" button.

## Expected Results

- The first row of the list shows "Newest task on top".
- The rows below show the older tasks, for example "Review security configuration".
