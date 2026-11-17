# TASK-LIST-005: An organization without tasks shows an empty message

| Field | Value |
|-------|-------|
| User Story | US-TASK-05 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/TaskList005Tests |

## Description

When the organization has no tasks, the page says so and invites an admin to create the first one.

## Preconditions

- Signed in as Admin of "Empty Org".
- "Empty Org" has no tasks.

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The page shows "No tasks yet" and "Create your first task to get started."
- No table of tasks is shown.
- The "New Task" button is shown.
