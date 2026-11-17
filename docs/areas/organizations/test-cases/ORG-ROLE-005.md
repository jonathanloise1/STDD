# ORG-ROLE-005: The only Admin cannot give up the Admin role

| Field | Value |
|-------|-------|
| User Story | US-ORG-06 |
| Type | Edge case |
| Priority | High |
| Automated by | e2e/OrgRole005Tests, integration/OrganizationsOutcomeCodesTests |

## Description

The only Admin of an organization who confirms a lower role for themselves is told that the organization needs an Admin, and keeps the Admin role.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- "Dev User" is the only Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Role | "Editor" |

## Steps

1. Click the "Team" link in the menu.
2. Click the "Edit" button on the row of "Dev User".
3. Check the "Editor" field.
4. Click the "Save" button.
5. Click the "Confirm Demotion" button.

## Expected Results

- The message "An organization needs at least one Admin." appears.
- The row of "Dev User" still shows the role "Admin".
- The menu still shows the "Team" link.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "An organization needs at least one Admin." |
| Log key | `org.role.last_admin` |
| Severity | Information |
| Runbook | [org-role-last-admin](../operations.md#org-role-last-admin) |
