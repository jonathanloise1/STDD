# TASK-CREATE-003: A new task starts with status Todo

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/TaskCreate003Tests |

## Description

Whatever the user types in the form, a new task is shown with the status "Todo".

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Book the meeting room" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Book the meeting room" in the "Title" field.
4. Click the "Create Task" button.

## Expected Results

- The first row of the list shows "Book the meeting room".
- The status of that row is "Todo".
- The "New Task" form has no Status field.
