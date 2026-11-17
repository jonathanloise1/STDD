# HELP-OPEN-003: The page about a form comes first in the panel

| Field | Value |
|-------|-------|
| User Story | US-HELP-03 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/HelpOpen003Tests |

## Description

The Help button of a form lists the page about that form before the other pages of the screen.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The New Task form has its own "Help" button.

## Test Data

None.

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Click the "Help" button of the "New Task" form.

## Expected Results

- The first entry of the list is "Create a task".
- The list also shows "See the list of tasks" after it.
