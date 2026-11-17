# AUTH-LOGOUT-001: Signing out opens the sign-in page

| Field | Value |
|-------|-------|
| User Story | US-AUTH-03 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/AuthLogout001Tests |

## Description

A signed-in user who clicks "Logout" in the avatar menu is signed out and sees the sign-in page.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Dev User" button.
3. Click the "Logout" button.

## Expected Results

- The avatar menu shows "Settings" and "Logout".
- The sign-in page opens.
