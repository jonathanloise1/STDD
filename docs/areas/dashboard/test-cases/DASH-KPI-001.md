# DASH-KPI-001: The dashboard shows the task counts by status

| Field | Value |
|-------|-------|
| User Story | US-DASH-01 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/DashKpi001Tests |

## Description

The four cards show the total and the number of tasks in each status, counting every task of an organization that has more than ten.

## Preconditions

- Signed in as Viewer of "Stats Org".
- "Stats Org" has 12 tasks: 5 with status "Todo", 3 with "In Progress", 3 with "Done" and 1 with "Cancelled".

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The card "Total Tasks" shows 12.
- The card "Todo" shows 5.
- The card "In Progress" shows 3.
- The card "Done" shows 3.
