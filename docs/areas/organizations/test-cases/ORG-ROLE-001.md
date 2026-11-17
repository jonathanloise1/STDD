# ORG-ROLE-001: Give a viewer the Editor role

| Field | Value |
|-------|-------|
| User Story | US-ORG-06 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/OrgRole001Tests |

## Description

An admin changes a member from Viewer to Editor and sees the new role in the table.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Role | "Editor" |
| Role (restored) | "Viewer" |

## Steps

1. Click the "Team" link in the menu.
2. Click the "Edit" button on the row of "Dev Viewer".
3. Check the "Editor" field.
4. Click the "Save" button.
5. Click the "Edit" button on the row of "Dev Viewer".
6. Check the "Viewer" field.
7. Click the "Save" button.

## Expected Results

- After the first save, the dialog closes and the message "Member updated successfully" appears.
- After the first save, the row of "Dev Viewer" shows the role "Editor".
- After the second save, the row of "Dev Viewer" shows the role "Viewer" again.
