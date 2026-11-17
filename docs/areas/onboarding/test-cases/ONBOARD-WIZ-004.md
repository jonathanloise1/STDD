# ONBOARD-WIZ-004: Creating when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-ONBOARD-02 |
| Type | Error handling |
| Priority | High |
| Automated by | e2e/OnboardWiz004Tests, integration/OnboardingOutcomeCodesTests |

## Description

When the service does not answer while the organization is created, the user is told so and the wizard keeps what was typed.

## Preconditions

- Not signed in.
- "New User" belongs to no organization.
- The service does not answer when an organization is created.

## Test Data

| Field | Value |
|-------|-------|
| Organization Name | "Initech" |

## Steps

1. Open the home page.
2. Sign in as "New User".
3. Type "Initech" in the "Organization Name" field.
4. Click the "Submit" button.

## Expected Results

- The message "The service did not answer. Try again in a few minutes." appears.
- The "Set Up Your Organization" page stays open and the "Organization Name" field still shows "Initech".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `onboard.wiz.unavailable` |
| Severity | Error |
| Runbook | [onboard-wiz-unavailable](../operations.md#onboard-wiz-unavailable) |
