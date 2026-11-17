# DASH-RECENT-003: Loading recent tasks when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-DASH-02 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/DashRecent003Tests, integration/DashboardOutcomeCodesTests |

## Description

When the service does not answer while the recent tasks are loaded, the section says so instead of claiming there are no tasks.

## Preconditions

- Signed in as Viewer of "Stats Org".
- The service does not answer when the recent tasks are loaded.

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The "Recent Tasks" section shows "The service did not answer. Try again in a few minutes."
- The section does not show "No tasks yet".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `dash.recent.unavailable` |
| Severity | Error |
| Runbook | [dash-recent-unavailable](../operations.md#dash-recent-unavailable) |
