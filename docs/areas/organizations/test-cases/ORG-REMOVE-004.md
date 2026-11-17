# ORG-REMOVE-004: Removing a member already removed shows a message

| Field | Value |
|-------|-------|
| User Story | US-ORG-07 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/OrgRemove004Tests, integration/OrganizationsOutcomeCodesTests |

## Description

An admin who confirms the removal of a member that another admin has just removed is told that the member is no longer in the team.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".
- "MyApp Dev Org" has a Viewer named "Former Member".

## Test Data

None.

## Steps

1. Click the "Team" link in the menu.
2. Click the "Remove" button on the row of "Former Member".
3. As Admin: Click the "Team" link in the menu.
4. As Admin: Click the "Remove" button on the row of "Former Member".
5. As Admin: Click the "Remove" button in the dialog.
6. Click the "Remove" button in the dialog.

## Expected Results

- The message "This member is no longer in the team." appears.
- The "Members" table does not show "Former Member".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "This member is no longer in the team." |
| Log key | `org.remove.not_found` |
| Severity | Warning |
| Runbook | [org-remove-not-found](../operations.md#org-remove-not-found) |
