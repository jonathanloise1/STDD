# TASK-CREATE-003: Saving after the role became Viewer shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Error handling |
| Priority | High |
| Automated by | None |

## Description

An editor whose role changed cannot save.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The message "Your role does not allow this change." appears.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "Your role does not allow this change." |
| Log key | `task.create.not_allowed` |
| Severity | Warning |
| Runbook | [task-create-not-allowed](../operations.md#task-create-not-allowed) |
