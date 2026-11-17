# ORG-VIEW-003: Loading the details when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-ORG-01 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/OrgView003Tests, integration/OrganizationsOutcomeCodesTests |

## Description

When the service does not answer while the organization details are loaded, the admin is told so.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The service does not answer when the organization details are loaded.

## Test Data

None.

## Steps

1. Click the "Settings" link in the menu.

## Expected Results

- The message "The service did not answer. Try again in a few minutes." appears.
- The page does not show "VAT Number".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `org.view.unavailable` |
| Severity | Error |
| Runbook | [org-view-unavailable](../operations.md#org-view-unavailable) |
