# AUTH-PROVISION-001: First sign-in goes straight into the app

| Field | Value |
|-------|-------|
| User Story | US-AUTH-04 |
| Type | Happy path |
| Priority | Critical |
| Automated by | e2e/AuthProvision001Tests |

## Description

A person who has never signed in can use the app at once, with no registration form, starting from the organization setup.

## Preconditions

- Not signed in.
- "New User" has never signed in and belongs to no organization.

## Test Data

None.

## Steps

1. Open the home page.
2. Sign in as "New User".

## Expected Results

- No registration form is shown.
- The "Set Up Your Organization" page opens.
