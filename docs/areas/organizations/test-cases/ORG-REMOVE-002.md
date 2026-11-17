# ORG-REMOVE-002: Admin rows have no remove button

| Field | Value |
|-------|-------|
| User Story | US-ORG-07 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/OrgRemove002Tests |

## Description

The table offers removal for Editors and Viewers but not for Admins.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Click the "Team" link in the menu.

## Expected Results

- The row of "Dev User" (Admin) has no "Remove" button.
- The row of "Dev Member" (Editor) has a "Remove" button.
- The row of "Dev Viewer" (Viewer) has a "Remove" button.
