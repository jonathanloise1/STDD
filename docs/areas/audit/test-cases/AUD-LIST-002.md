# AUD-LIST-002: A new task appears at the top of the list

| Field | Value |
|-------|-------|
| User Story | US-AUD-01 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/AudList002Tests |

## Description

Creating a task adds a change at the top of the list with the action Create and the name of the admin who made it.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Audit trail check" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Audit trail check" in the "Title" field.
4. Click the "Create Task" button.
5. Click the "Audit Log" link in the menu.

## Expected Results

- The first row of the table shows the action "Create" and the type "Task".
- The "User" column of the first row shows "Dev User".
- The "Changes" column of the first row shows "-".
