# DASH-RECENT-001: Recent tasks show title, status and due date

| Field | Value |
|-------|-------|
| User Story | US-DASH-02 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/DashRecent001Tests |

## Description

The Recent Tasks table lists tasks with their title, status label and due date.

## Preconditions

- Signed in as Viewer of "Stats Org".
- "Stats Org" has the tasks described in the test suite; "Stats task 12" is the newest, is cancelled and has no due date.

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The "Recent Tasks" section has the columns "Title", "Status" and "Due Date".
- The row of "Stats task 12" shows the status "Cancelled" and "-" in the "Due Date" column.
- The row of "Stats task 11" shows the status "Done" and a due date.
- The row of "Stats task 08" shows the status "In Progress".
