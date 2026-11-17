# AUD-DETAIL-003: Cancel closes the details dialog

| Field | Value |
|-------|-------|
| User Story | US-AUD-03 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/AudDetail003Tests |

## Description

Closing the dialog returns to the list unchanged.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- "MyApp Dev Org" has changes on record.

## Test Data

None.

## Steps

1. Click the "Audit Log" link in the menu.
2. Click the "Details" button on the first row of the table.
3. Click the "Cancel" button.

## Expected Results

- The "Details" dialog is closed.
- The table still shows the same rows.
