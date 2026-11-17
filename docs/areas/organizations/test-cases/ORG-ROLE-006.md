# ORG-ROLE-006: Saving the role of a removed member shows a message

| Field | Value |
|-------|-------|
| User Story | US-ORG-06 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/OrgRole006Tests, integration/OrganizationsOutcomeCodesTests |

## Description

An admin who saves a new role for a member that another admin has just removed is told that the member is no longer in the team.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".
- "MyApp Dev Org" has a Viewer named "Leaving Member".

## Test Data

| Field | Value |
|-------|-------|
| Role | "Editor" |

## Steps

1. Click the "Team" link in the menu.
2. Click the "Edit" button on the row of "Leaving Member".
3. Check the "Editor" field.
4. As Admin: Click the "Team" link in the menu.
5. As Admin: Click the "Remove" button on the row of "Leaving Member".
6. As Admin: Click the "Remove" button in the dialog.
7. Click the "Save" button.

## Expected Results

- The message "This member is no longer in the team." appears.
- The "Members" table does not show "Leaving Member".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "This member is no longer in the team." |
| Log key | `org.role.not_found` |
| Severity | Warning |
| Runbook | [org-role-not-found](../operations.md#org-role-not-found) |
