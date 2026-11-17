# AUTH-PROVISION-002: Signing in again does not create a second account

| Field | Value |
|-------|-------|
| User Story | US-AUTH-04 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/AuthProvision002Tests |

## Description

A returning user who signs in a second time finds the same account and the same single organization.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Dev User" button.
3. Click the "Logout" button.
4. Sign in as Admin.

## Expected Results

- The Dashboard page opens.
- The header shows "MyApp Dev Org" once, without a list of other organizations.
- The "Set Up Your Organization" page is not shown.
