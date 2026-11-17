# TASK-CREATE-001: Create a task with a title

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Happy path |
| Priority | Critical |
| Automated by | e2e/TaskCreate001Tests |

## Description

An editor adds a task with a title, a due date and a description, and finds it in the list.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Prepare the quarterly report" |
| Due Date | "2030-01-15" |
| Description | "Collect the figures from every team" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Prepare the quarterly report" in the "Title" field.
4. Type "2030-01-15" in the "Due Date" field.
5. Type "Collect the figures from every team" in the "Description" field.
6. Click the "Create Task" button.

## Expected Results

- The form closes.
- The first row of the list shows "Prepare the quarterly report" with the description "Collect the figures from every team".
- The first row shows the status "Todo" and the due date "2030-01-15".
