# ORG-INVITE-007: The invitation dialog starts with the Viewer role

| Field | Value |
|-------|-------|
| User Story | US-ORG-05 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/OrgInvite007Tests |

## Description

The role Viewer is selected when the dialog opens.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Click the "Team" link in the menu.
2. Click the "Invite" button.

## Expected Results

- The "Invite Team Member" dialog shows "Admin", "Editor" and "Viewer", each with a description.
- "Viewer" is selected.
