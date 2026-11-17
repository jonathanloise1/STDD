# AUD-LIST-004: Next and Previous move between pages of 25 changes

| Field | Value |
|-------|-------|
| User Story | US-AUD-01 |
| Type | UX |
| Priority | Medium |
| Automated by | e2e/AudList004Tests |

## Description

With more than 25 changes, the admin can move between pages.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- "MyApp Dev Org" has more than 25 changes on record.

## Test Data

None.

## Steps

1. Click the "Audit Log" link in the menu.
2. Click the "Next" button.
3. Click the "Previous" button.

## Expected Results

- After step 2, the page position starts with "2 /" and the table shows the next changes.
- After step 3, the page position starts with "1 /" and the table shows 25 rows.
