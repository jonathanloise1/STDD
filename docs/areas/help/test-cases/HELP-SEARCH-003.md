# HELP-SEARCH-003: The results show at most ten sections

| Field | Value |
|-------|-------|
| User Story | US-HELP-02 |
| Type | Business rule |
| Priority | Low |
| Automated by | e2e/HelpSearch003Tests |

## Description

A search for a word that many sections contain still lists no more than ten.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- More than ten sections of the manual contain the word "task".

## Test Data

| Field | Value |
|-------|-------|
| Search the manual | "task" |

## Steps

1. Open the Manual page.
2. Type "task" in the "Search the manual" field.
3. Click the "Search" button.

## Expected Results

- The results list shows exactly ten sections.
- The best matching page is first in the list.
