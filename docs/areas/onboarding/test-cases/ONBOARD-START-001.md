# ONBOARD-START-001: A user without an organization is taken to the setup page

| Field | Value |
|-------|-------|
| User Story | US-ONBOARD-01 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/OnboardStart001Tests |

## Description

A user who belongs to no organization sees the setup page right after signing in, with no way to skip it.

## Preconditions

- Not signed in.
- "New User" belongs to no organization.

## Test Data

None.

## Steps

1. Open the home page.
2. Sign in as "New User".

## Expected Results

- The "Set Up Your Organization" page opens.
- The wizard shows the step "Organization Details" and a "Submit" button.
- No "Skip for now" link is shown.
