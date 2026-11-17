# AUTH-LOGIN-006: Signing in when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-AUTH-01 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/AuthLogin006Tests, integration/AuthenticationOutcomeCodesTests |

## Description

When the service does not answer while the account is loaded after signing in, the user is told so instead of seeing an empty app.

## Preconditions

- Not signed in.
- The service does not answer when the account is loaded after signing in.

## Test Data

None.

## Steps

1. Open the home page.
2. Sign in as Admin.

## Expected Results

- The message "The service did not answer. Try again in a few minutes." appears.
- The header does not show "MyApp Dev Org".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `auth.login.unavailable` |
| Severity | Error |
| Runbook | [auth-login-unavailable](../operations.md#auth-login-unavailable) |
