# HELP-OPEN-008: The panel shows a message when the help cannot be loaded

| Field | Value |
|-------|-------|
| User Story | US-HELP-03 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/HelpOpen008Tests |

## Description

When the help content cannot be loaded, the panel says so and offers the manual.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The help content cannot be loaded.

## Test Data

None.

## Steps

1. Open the Tasks page.
2. Click the "Help" button.

## Expected Results

- The message "Help is unavailable." appears in the panel.
- The panel shows an "Open the manual" link.
- The Tasks page is still usable.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "Help is unavailable." |
| Log key | `help.open.unavailable` |
| Severity | Error |
| Runbook | [help-open-unavailable](../operations.md#help-open-unavailable) |
