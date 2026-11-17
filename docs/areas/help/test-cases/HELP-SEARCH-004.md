# HELP-SEARCH-004: A search with no match shows a message and a link

| Field | Value |
|-------|-------|
| User Story | US-HELP-02 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/HelpSearch004Tests |

## Description

When no section matches, the page says so and offers a way back to the manual.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Search the manual | "zzqxj" |

## Steps

1. Open the Manual page.
2. Type "zzqxj" in the "Search the manual" field.
3. Click the "Search" button.

## Expected Results

- The message "Nothing found in the manual." appears.
- The page shows a "Go to the manual home" link.
- Clicking the link opens the home page of the manual.
