# ONBOARD-START-003: A visitor cannot open the setup page

| Field | Value |
|-------|-------|
| User Story | US-ONBOARD-01 |
| Type | Security |
| Priority | High |
| Automated by | e2e/OnboardStart003Tests |

## Description

Someone who is not signed in is asked to sign in instead of seeing the setup page.

## Preconditions

- Not signed in.

## Test Data

None.

## Steps

1. Open the Set Up Your Organization page.

## Expected Results

- The sign-in page opens.
- The text "Set Up Your Organization" is not shown.
