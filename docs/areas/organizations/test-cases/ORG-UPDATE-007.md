# ORG-UPDATE-007: Saving when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-ORG-02 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/OrgUpdate007Tests, integration/OrganizationsOutcomeCodesTests |

## Description

When the service does not answer while the details are saved, the admin is told so and the page keeps the values typed.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The service does not answer when the organization details are saved.

## Test Data

| Field | Value |
|-------|-------|
| City | "Bergamo" |

## Steps

1. Click the "Settings" link in the menu.
2. Click the "Edit" button.
3. Clear the "City" field.
4. Type "Bergamo" in the "City" field.
5. Click the "Save Changes" button.

## Expected Results

- The message "The service did not answer. Try again in a few minutes." appears.
- The Update Organization page stays open and the "City" field still shows "Bergamo".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `org.update.unavailable` |
| Severity | Error |
| Runbook | [org-update-unavailable](../operations.md#org-update-unavailable) |
