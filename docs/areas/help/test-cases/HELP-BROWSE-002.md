# HELP-BROWSE-002: The pages of an area follow the order of the stories

| Field | Value |
|-------|-------|
| User Story | US-HELP-01 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/HelpBrowse002Tests |

## Description

The area page lists its pages in the order of the stories they describe.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Manual page.
2. Click the "Tasks" link.

## Expected Results

- The list shows the pages in this order: "Create a task", "Edit a task", "Change the status of a task", "Delete a task", "See the list of tasks".
