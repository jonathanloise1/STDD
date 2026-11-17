# HELP-DOWNLOAD-007: An editor cannot see or open the unanswered questions

| Field | Value |
|-------|-------|
| User Story | US-HELP-07 |
| Type | Security |
| Priority | High |
| Automated by | e2e/HelpDownload007Tests |

## Description

An Editor has no menu item and is refused when opening the page by its address.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Open the Unanswered questions page.

## Expected Results

- The menu does not show "Unanswered questions".
- The message "You don't have access to this page." appears.
- The "Download" button is not shown.
