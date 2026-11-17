# HELP-SEARCH-002: A search finds other forms of the same word

| Field | Value |
|-------|-------|
| User Story | US-HELP-02 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/HelpSearch002Tests |

## Description

A search for the plural finds sections that use the singular, and the matching words are bold.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Search the manual | "tasks" |

## Steps

1. Open the Manual page.
2. Type "tasks" in the "Search the manual" field.
3. Click the "Search" button.

## Expected Results

- A result shows the page title "Create a task".
- In the results, the word "task" is shown in bold.
