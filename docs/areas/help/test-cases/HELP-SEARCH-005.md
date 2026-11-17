# HELP-SEARCH-005: A visitor cannot search the manual

| Field | Value |
|-------|-------|
| User Story | US-HELP-02 |
| Type | Security |
| Priority | High |
| Automated by | e2e/HelpSearch005Tests |

## Description

A person who is not signed in cannot reach the search results.

## Preconditions

- Not signed in.

## Test Data

None.

## Steps

1. Open the Manual page.

## Expected Results

- The Sign in page opens.
- The "Search the manual" field is not shown.
