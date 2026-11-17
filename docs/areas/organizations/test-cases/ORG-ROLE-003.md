# ORG-ROLE-003: Lowering your own role asks for confirmation

| Field | Value |
|-------|-------|
| User Story | US-ORG-06 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/OrgRole003Tests |

## Description

An admin who lowers their own role sees a warning and must confirm; going back leaves the role unchanged.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Role | "Editor" |

## Steps

1. Click the "Team" link in the menu.
2. Click the "Edit" button on the row of "Dev User".
3. Check the "Editor" field.
4. Click the "Save" button.
5. Click the "Go Back" button.
6. Click the "Cancel" button.

## Expected Results

- After step 3 the dialog shows "You are removing your own Admin privileges. This action cannot be undone by yourself."
- After step 4 the dialog shows the buttons "Go Back" and "Confirm Demotion".
- After step 6 the row of "Dev User" still shows the role "Admin".
