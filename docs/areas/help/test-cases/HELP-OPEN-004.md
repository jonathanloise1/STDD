# HELP-OPEN-004: A screen without a manual page shows the manual home

| Field | Value |
|-------|-------|
| User Story | US-HELP-03 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/HelpOpen004Tests |

## Description

A screen with no page of its own and no area page lists the manual home page.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The manual has no page for the Billing page and no area page for its path.

## Test Data

None.

## Steps

1. Open the Billing page.
2. Click the "Help" button.

## Expected Results

- The list shows one entry, the home page of the manual.
