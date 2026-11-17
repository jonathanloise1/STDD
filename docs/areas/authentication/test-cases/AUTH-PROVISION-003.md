# AUTH-PROVISION-003: A new account starts in English

| Field | Value |
|-------|-------|
| User Story | US-AUTH-04 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/AuthProvision003Tests |

## Description

A new user who has chosen no language sees the app in English.

## Preconditions

- Not signed in.
- "New User" has never signed in and belongs to no organization.

## Test Data

None.

## Steps

1. Open the home page.
2. Sign in as "New User".

## Expected Results

- The page title "Set Up Your Organization" is shown in English.
- The "Submit" button is shown in English.
