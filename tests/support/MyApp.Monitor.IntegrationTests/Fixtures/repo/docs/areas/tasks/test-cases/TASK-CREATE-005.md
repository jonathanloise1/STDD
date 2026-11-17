# TASK-CREATE-005: Saving after access was removed shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Error handling |
| Priority | High |
| Automated by | None |

## Description

A member removed from the organization while the form is open is told why the task is not saved.

## Preconditions

- Signed in as Editor of "Fixture Org".

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The message "You no longer have access to this organization." appears.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "You no longer have access to this organization." |
| Log key | `task.create.access_lost` |
| Severity | Warning |
| Runbook | [task-create-access-lost](../operations.md#task-create-access-lost) |
