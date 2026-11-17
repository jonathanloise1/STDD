# ORG-LOGO-004: Uploading when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-ORG-03 |
| Type | Error handling |
| Priority | Low |
| Automated by | e2e/OrgLogo004Tests, integration/OrganizationsOutcomeCodesTests |

## Description

When the service does not answer while the logo is uploaded, the admin is told so and the logo does not change.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The service does not answer when a logo is uploaded.

## Test Data

| Field | Value |
|-------|-------|
| Logo file | "logo.png" |

## Steps

1. Click the "Settings" link in the menu.
2. Click the "Edit" button.
3. Upload the file "logo.png" with the "Click to upload logo" button.

## Expected Results

- The message "The service did not answer. Try again in a few minutes." appears.
- The logo does not change.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `org.logo.unavailable` |
| Severity | Error |
| Runbook | [org-logo-unavailable](../operations.md#org-logo-unavailable) |
