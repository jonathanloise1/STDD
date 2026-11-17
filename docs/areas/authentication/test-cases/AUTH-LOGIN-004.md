# AUTH-LOGIN-004: Reloading the page keeps the user signed in

| Field | Value |
|-------|-------|
| User Story | US-AUTH-01 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/AuthLogin004Tests |

## Description

A signed-in user who reloads the page stays in the app and is not asked to sign in again.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Tasks page.
2. Reload the page.

## Expected Results

- The Tasks page is still shown after the reload.
- The sign-in page does not open.
