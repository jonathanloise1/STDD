# ONBOARD-START-004: Any page opened without an organization shows the setup page

| Field | Value |
|-------|-------|
| User Story | US-ONBOARD-01 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/OnboardStart004Tests |

## Description

A user who belongs to no organization and opens another page of the app sees the setup page instead.

## Preconditions

- Not signed in.
- "New User" belongs to no organization.

## Test Data

None.

## Steps

1. Open the Tasks page.
2. Sign in as "New User".

## Expected Results

- The "Set Up Your Organization" page opens.
- The Tasks page is not shown.
