# AUD-LIST-003: An organization without changes shows an empty message

| Field | Value |
|-------|-------|
| User Story | US-AUD-01 |
| Type | UX |
| Priority | Medium |
| Automated by | e2e/AudList003Tests |

## Description

When the organization has no recorded changes, the table says so.

## Preconditions

- Signed in as Admin of "Empty Org".
- "Empty Org" has no changes on record.

## Test Data

None.

## Steps

1. Click the "Audit Log" link in the menu.

## Expected Results

- The table shows "No audit logs found."
- No "Previous" or "Next" button is shown.
