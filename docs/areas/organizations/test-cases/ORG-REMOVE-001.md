# ORG-REMOVE-001: Remove a member from the team

| Field | Value |
|-------|-------|
| User Story | US-ORG-07 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/OrgRemove001Tests |

## Description

An admin removes a viewer after confirming and the member leaves the table.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- "MyApp Dev Org" has a Viewer named "Spare Member".

## Test Data

None.

## Steps

1. Click the "Team" link in the menu.
2. Click the "Remove" button on the row of "Spare Member".
3. Click the "Remove" button in the dialog.

## Expected Results

- The dialog "Confirm Removal" asked "Are you sure you want to remove Spare Member from the team? This action cannot be undone."
- The message "Member removed successfully" appears.
- The "Members" table does not show "Spare Member".
