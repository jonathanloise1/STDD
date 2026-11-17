# DASH-KPI-002: Cancelled tasks are counted only in the total

| Field | Value |
|-------|-------|
| User Story | US-DASH-01 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/DashKpi002Tests |

## Description

A cancelled task raises "Total Tasks" but none of the status cards, so the total is one more than the sum of the other three cards.

## Preconditions

- Signed in as Viewer of "Stats Org".
- "Stats Org" has 12 tasks, one of them with status "Cancelled".

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The card "Total Tasks" shows 12.
- The cards "Todo", "In Progress" and "Done" together show 11.
