# HELP-BROWSE-004: A visitor cannot read the manual

| Field | Value |
|-------|-------|
| User Story | US-HELP-01 |
| Type | Security |
| Priority | High |
| Automated by | e2e/HelpBrowse004Tests |

## Description

A person who is not signed in is asked to sign in instead of seeing the manual.

## Preconditions

- Not signed in.

## Test Data

None.

## Steps

1. Open the Manual page.

## Expected Results

- The Sign in page opens.
- The list of areas is not shown.
