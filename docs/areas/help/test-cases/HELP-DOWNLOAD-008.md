# HELP-DOWNLOAD-008: A viewer cannot see or open the unanswered questions

| Field | Value |
|-------|-------|
| User Story | US-HELP-07 |
| Type | Security |
| Priority | High |
| Automated by | e2e/HelpDownload008Tests |

## Description

A Viewer has no menu item and is refused when opening the page by its address.

## Preconditions

- Signed in as Viewer of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Open the Unanswered questions page.

## Expected Results

- The menu does not show "Unanswered questions".
- The message "You don't have access to this page." appears.
- The "Download" button is not shown.
