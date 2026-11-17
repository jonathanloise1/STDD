# AUD-LIST-007: Loading the changes when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-AUD-01 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/AudList007Tests, integration/AuditOutcomeCodesTests |

## Description

When the service does not answer while the changes are loaded, the admin is told so instead of seeing an empty history.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The service does not answer when the audit trail is loaded.

## Test Data

None.

## Steps

1. Click the "Audit Log" link in the menu.

## Expected Results

- The message "The service did not answer. Try again in a few minutes." appears.
- The table does not show "No audit logs found."

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `aud.list.unavailable` |
| Severity | Error |
| Runbook | [aud-list-unavailable](../operations.md#aud-list-unavailable) |
