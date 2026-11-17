# HELP-OPEN-002: Clicking a page in the panel opens it in the manual

| Field | Value |
|-------|-------|
| User Story | US-HELP-03 |
| Type | Happy path |
| Priority | Medium |
| Automated by | e2e/HelpOpen002Tests |

## Description

A page chosen in the panel opens in the manual.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Create a task" link.

## Expected Results

- The Manual page shows the page "Create a task".
- The section "Steps" is shown on the page.
