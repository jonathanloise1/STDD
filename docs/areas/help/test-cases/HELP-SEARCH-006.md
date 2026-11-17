# HELP-SEARCH-006: A search shows a message when the manual cannot be loaded

| Field | Value |
|-------|-------|
| User Story | US-HELP-02 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/HelpSearch006Tests |

## Description

When the content needed for the search cannot be loaded, the search says help is unavailable.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The content of the manual cannot be loaded for the search.

## Test Data

| Field | Value |
|-------|-------|
| Search the manual | "create task" |

## Steps

1. Open the Manual page.
2. Type "create task" in the "Search the manual" field.
3. Click the "Search" button.

## Expected Results

- The message "Help is unavailable." appears.
- The results list is not shown.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "Help is unavailable." |
| Log key | `help.search.unavailable` |
| Severity | Error |
| Runbook | [help-search-unavailable](../operations.md#help-search-unavailable) |
