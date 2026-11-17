# DASH-KPI-004: Loading the counts when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-DASH-01 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/DashKpi004Tests, integration/DashboardOutcomeCodesTests |

## Description

When the service does not answer while the counts are loaded, the dashboard says so instead of showing zeros.

## Preconditions

- Signed in as Viewer of "Stats Org".
- The service does not answer when the task counts are loaded.

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The message "The service did not answer. Try again in a few minutes." is shown in place of the cards.
- The card "Total Tasks" does not show 0.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `dash.kpi.unavailable` |
| Severity | Error |
| Runbook | [dash-kpi-unavailable](../operations.md#dash-kpi-unavailable) |
