# TASK-CREATE-008: Saving while the service fails shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Error handling |
| Priority | High |
| Automated by | None |

## Description

When the service fails the user is told to try again later.

## Preconditions

- Signed in as Editor of "Fixture Org".

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The message "The service is unavailable. Please try again later." appears.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service is unavailable. Please try again later." |
| Log key | `task.create.unavailable` |
| Severity | Error |
| Runbook | [task-create-unavailable](../operations.md#task-create-unavailable) |
