# HELP-SEARCH-001: Searching shows the matching sections of the manual

| Field | Value |
|-------|-------|
| User Story | US-HELP-02 |
| Type | Happy path |
| Priority | Medium |
| Automated by | e2e/HelpSearch001Tests |

## Description

A search for two words lists the sections that match, each with its page, heading and a short text.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Search the manual | "create task" |

## Steps

1. Open the Manual page.
2. Type "create task" in the "Search the manual" field.
3. Click the "Search" button.

## Expected Results

- The results include a result with the page title "Create a task".
- Each result shows a page title, a section heading and a short text of at most 160 characters.
- Clicking the result opens the page "Create a task" in the manual.
