# DASH-ACTIONS-002: Clicking a recent task row opens the task list

| Field | Value |
|-------|-------|
| User Story | US-DASH-03 |
| Type | Happy path |
| Priority | Low |
| Automated by | e2e/DashActions002Tests |

## Description

Clicking a row of the Recent Tasks table takes the user to the Tasks page.

## Preconditions

- Signed in as Viewer of "Stats Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the row of "Stats task 12" in the "Recent Tasks" table.

## Expected Results

- The Tasks page opens.
- The list shows "Stats task 12".
