# AUTH-LOGOUT-002: After signing out, a page asks to sign in again

| Field | Value |
|-------|-------|
| User Story | US-AUTH-03 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/AuthLogout002Tests |

## Description

Once signed out, the app cannot be used: opening a page asks the user to sign in again.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Dev User" button.
3. Click the "Logout" button.
4. Open the Tasks page.

## Expected Results

- The sign-in page opens.
- The Tasks page is not shown.
