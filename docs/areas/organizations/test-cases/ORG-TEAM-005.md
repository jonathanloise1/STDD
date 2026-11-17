# ORG-TEAM-005: Loading the team when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-ORG-04 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/OrgTeam005Tests, integration/OrganizationsOutcomeCodesTests |

## Description

When the service does not answer while the members are loaded, the admin is told so instead of seeing an empty team.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The service does not answer when the team members are loaded.

## Test Data

None.

## Steps

1. Click the "Team" link in the menu.

## Expected Results

- The message "The service did not answer. Try again in a few minutes." appears.
- The "Members" table does not show "No team members found".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `org.team.unavailable` |
| Severity | Error |
| Runbook | [org-team-unavailable](../operations.md#org-team-unavailable) |
