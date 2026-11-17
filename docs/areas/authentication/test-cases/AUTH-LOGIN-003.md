# AUTH-LOGIN-003: The sign-in page asks for an email and no password

| Field | Value |
|-------|-------|
| User Story | US-AUTH-01 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/AuthLogin003Tests |

## Description

The sign-in page offers email and code only: it has no password field and no sign-in with another service.

## Preconditions

- Not signed in.

## Test Data

None.

## Steps

1. Open the home page.

## Expected Results

- The sign-in page asks for an email address.
- The sign-in page has no password field.
- The sign-in page has no button to sign in with another service.
