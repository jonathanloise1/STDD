# ONBOARD-WIZ-003: A name made only of spaces counts as empty

| Field | Value |
|-------|-------|
| User Story | US-ONBOARD-02 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/OnboardWiz003Tests |

## Description

Spaces alone are not accepted as an organization name.

## Preconditions

- Not signed in.
- "New User" belongs to no organization.

## Test Data

| Field | Value |
|-------|-------|
| Organization Name | "   " |

## Steps

1. Open the home page.
2. Sign in as "New User".
3. Type "   " in the "Organization Name" field.
4. Click the "Submit" button.

## Expected Results

- The message "Organization name is required" appears.
- The text "Creating your organization..." is not shown.
- The "Set Up Your Organization" page stays open.
