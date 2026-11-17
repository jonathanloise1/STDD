# AUTH-LOGIN-002: Opening the Tasks page when not signed in asks to sign in

| Field | Value |
|-------|-------|
| User Story | US-AUTH-02 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/AuthLogin002Tests |

## Description

A visitor who opens the Tasks page is taken to the sign-in page, and after signing in the Tasks page opens.

## Preconditions

- Not signed in.

## Test Data

None.

## Steps

1. Open the Tasks page.
2. Sign in as Editor.

## Expected Results

- After step 1, the sign-in page opens and asks for an email address; the Tasks page is not shown.
- After step 2, the Tasks page opens.
