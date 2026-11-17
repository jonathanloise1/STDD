# HELP-OPEN-001: The Help button opens the pages of the current screen

| Field | Value |
|-------|-------|
| User Story | US-HELP-03 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/HelpOpen001Tests |

## Description

On the Tasks page, the Help button opens a panel that lists the manual pages of that screen.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Tasks page.
2. Click the "Help" button.

## Expected Results

- A side panel opens next to the Tasks page with the tabs "This page" and "Ask".
- The tab "This page" is open.
- The list shows the page "Create a task".
- The Tasks page is still shown behind the panel.
