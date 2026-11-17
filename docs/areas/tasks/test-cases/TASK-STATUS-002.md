# TASK-STATUS-002: The status goes round from Todo to Done and back

| Field | Value |
|-------|-------|
| User Story | US-TASK-03 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/TaskStatus002Tests |

## Description

Each click moves the task to the next status; after "Done" the task is back to "Todo".

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Archive old reports" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Archive old reports" in the "Title" field.
4. Click the "Create Task" button.
5. Click the "Change status" button on the row of "Archive old reports".
6. Click the "Change status" button on the row of "Archive old reports".
7. Click the "Change status" button on the row of "Archive old reports".

## Expected Results

- After the second click the row shows the status "Done".
- After the third click the row shows the status "Todo".
