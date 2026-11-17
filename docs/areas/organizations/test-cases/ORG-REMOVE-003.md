# ORG-REMOVE-003: Cancelling the removal keeps the member

| Field | Value |
|-------|-------|
| User Story | US-ORG-07 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/OrgRemove003Tests |

## Description

Clicking Cancel in the confirmation dialog closes it and the member stays in the table.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Click the "Team" link in the menu.
2. Click the "Remove" button on the row of "Dev Viewer".
3. Click the "Cancel" button in the dialog.

## Expected Results

- The dialog is closed.
- The "Members" table still shows "Dev Viewer".
