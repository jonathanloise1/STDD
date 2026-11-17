# AUTH-LOGIN-001: Signing in opens the Dashboard page

| Field | Value |
|-------|-------|
| User Story | US-AUTH-01 |
| Type | Happy path |
| Priority | Critical |
| Automated by | e2e/AuthLogin001Tests |

## Description

A member who signs in with their email address and code reaches the Dashboard page and is shown as signed in.

## Preconditions

- Not signed in.
- "Dev User" is the Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the home page.
2. Sign in as Admin.

## Expected Results

- The Dashboard page opens.
- The "Dev User" button at the top right shows "DU".
- The header shows "MyApp Dev Org".
