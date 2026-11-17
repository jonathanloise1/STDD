# ONBOARD-START-002: A member of an organization is not sent to the setup page

| Field | Value |
|-------|-------|
| User Story | US-ONBOARD-01 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/OnboardStart002Tests |

## Description

A user who belongs to an organization reaches the dashboard directly.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The Dashboard page opens.
- The text "Set Up Your Organization" is not shown.
