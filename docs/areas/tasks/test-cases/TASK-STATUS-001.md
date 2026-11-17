# TASK-STATUS-001: Move a task from Todo to In Progress

| Field | Value |
|-------|-------|
| User Story | US-TASK-03 |
| Type | Happy path |
| Priority | Medium |
| Automated by | e2e/TaskStatus001Tests |

## Description

Clicking the "Change status" button of a task with status "Todo" changes its status to "In Progress".

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Send the weekly update" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Send the weekly update" in the "Title" field.
4. Click the "Create Task" button.
5. Click the "Change status" button on the row of "Send the weekly update".

## Expected Results

- The row of "Send the weekly update" shows the status "In Progress".
