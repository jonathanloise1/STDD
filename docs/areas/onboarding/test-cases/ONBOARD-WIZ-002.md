# ONBOARD-WIZ-002: Submitting without a name shows a message

| Field | Value |
|-------|-------|
| User Story | US-ONBOARD-02 |
| Type | Validation |
| Priority | High |
| Automated by | e2e/OnboardWiz002Tests |

## Description

The wizard does not create an organization when the name is empty.

## Preconditions

- Not signed in.
- "New User" belongs to no organization.

## Test Data

| Field | Value |
|-------|-------|
| Billing Email | "billing@initech.test" |

## Steps

1. Open the home page.
2. Sign in as "New User".
3. Type "billing@initech.test" in the "Billing Email" field.
4. Click the "Submit" button.

## Expected Results

- The message "Organization name is required" appears.
- The "Set Up Your Organization" page stays open.
