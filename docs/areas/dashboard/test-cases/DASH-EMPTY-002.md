# DASH-EMPTY-002: An organization without tasks shows zero on every card

| Field | Value |
|-------|-------|
| User Story | US-DASH-01 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/DashEmpty002Tests |

## Description

All four cards show 0 when the organization has no tasks.

## Preconditions

- Signed in as Admin of "Empty Org".
- "Empty Org" has no tasks.

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The cards "Total Tasks", "Todo", "In Progress" and "Done" all show 0.
