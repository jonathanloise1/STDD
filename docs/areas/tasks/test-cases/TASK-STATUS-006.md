# TASK-STATUS-006: Changing the status keeps the other details of the task

| Field | Value |
|-------|-------|
| User Story | US-TASK-03 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/TaskStatus006Tests |

## Description

The "Change status" button changes only the status: the description and the due date of the task are kept.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Order business cards" |
| Due Date | "2030-03-10" |
| Description | "Ask the printer for a quote" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Order business cards" in the "Title" field.
4. Type "2030-03-10" in the "Due Date" field.
5. Type "Ask the printer for a quote" in the "Description" field.
6. Click the "Create Task" button.
7. Click the "Change status" button on the row of "Order business cards".
8. Reload the page.

## Expected Results

- The row of "Order business cards" shows the status "In Progress".
- The same row still shows the description "Ask the printer for a quote" and the due date "2030-03-10".
