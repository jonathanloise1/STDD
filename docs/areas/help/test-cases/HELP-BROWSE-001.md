# HELP-BROWSE-001: Open the manual and read a page

| Field | Value |
|-------|-------|
| User Story | US-HELP-01 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/HelpBrowse001Tests |

## Description

A user opens the Manual page, goes to an area and reads one of its pages.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Manual page.
2. Click the "Tasks" link.
3. Click the "Create a task" link.

## Expected Results

- The Manual page shows the home page with a list of areas that includes "Tasks".
- The area page lists the pages of the Tasks area as links.
- The page "Create a task" shows its title, an introduction and the sections "Steps" and "What you will see".
