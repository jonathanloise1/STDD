# HELP-ASK-006: The fixed reply links to a search for the same words

| Field | Value |
|-------|-------|
| User Story | US-HELP-04 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/HelpAsk006Tests |

## Description

The search link of the fixed reply opens the search results for the question.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The manual says nothing about paying invoices.

## Test Data

| Field | Value |
|-------|-------|
| Your question | "Can I pay my invoice from here?" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "Can I pay my invoice from here?" in the "Your question" field.
5. Click the "Ask" button.
6. Click the "Search the manual for this" link.

## Expected Results

- The Manual page shows the search results.
- The "Search the manual" field shows "Can I pay my invoice from here?".
