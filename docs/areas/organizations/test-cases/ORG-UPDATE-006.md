# ORG-UPDATE-006: The Unsaved Changes badge follows the edited values

| Field | Value |
|-------|-------|
| User Story | US-ORG-02 |
| Type | Business rule |
| Priority | Low |
| Automated by | e2e/OrgUpdate006Tests |

## Description

The badge appears when a value differs from the saved one and disappears when the original value is typed back.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Legal Name | "Changed Legal Name" |
| Legal Name (original) | "MyApp Dev Org S.r.l." |

## Steps

1. Click the "Settings" link in the menu.
2. Click the "Edit" button.
3. Clear the "Legal Name" field.
4. Type "Changed Legal Name" in the "Legal Name" field.
5. Clear the "Legal Name" field.
6. Type "MyApp Dev Org S.r.l." in the "Legal Name" field.

## Expected Results

- After step 4 the badge "Unsaved Changes" is shown next to the page title.
- After step 6 the badge "Unsaved Changes" is no longer shown.
