# DASH-RECENT-002: Only the five newest tasks are listed

| Field | Value |
|-------|-------|
| User Story | US-DASH-02 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/DashRecent002Tests |

## Description

With more than five tasks, the table shows the five most recently created, newest first.

## Preconditions

- Signed in as Viewer of "Stats Org".
- "Stats Org" has twelve tasks "Stats task 01" to "Stats task 12", created in that order.

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The "Recent Tasks" table shows five rows.
- The first row is "Stats task 12" and the last row is "Stats task 08".
- "Stats task 07" and "Stats task 01" are not listed.
