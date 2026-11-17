# HELP-BROWSE-005: The manual shows a message when its content cannot be loaded

| Field | Value |
|-------|-------|
| User Story | US-HELP-01 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/HelpBrowse005Tests |

## Description

When the content of the manual cannot be loaded, the page says so instead of staying blank.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The content of the manual cannot be loaded.

## Test Data

None.

## Steps

1. Open the Manual page.

## Expected Results

- The message "The manual could not be loaded." appears.
- The list of areas is not shown.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The manual could not be loaded." |
| Log key | `help.browse.load_failed` |
| Severity | Error |
| Runbook | [help-browse-load-failed](../operations.md#help-browse-load-failed) |
