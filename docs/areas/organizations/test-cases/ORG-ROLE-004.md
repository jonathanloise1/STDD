# ORG-ROLE-004: Save stays disabled until something changes

| Field | Value |
|-------|-------|
| User Story | US-ORG-06 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/OrgRole004Tests |

## Description

The Save button is disabled when the dialog opens and enabled after a role is chosen.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Role | "Editor" |

## Steps

1. Click the "Team" link in the menu.
2. Click the "Edit" button on the row of "Dev Viewer".
3. Check the "Editor" field.

## Expected Results

- When the dialog opens, "Viewer" is selected and the "Save" button is disabled.
- After step 3 the "Save" button is enabled.
