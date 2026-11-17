# ONBOARD-WIZ-001: Submitting the details creates the organization

| Field | Value |
|-------|-------|
| User Story | US-ONBOARD-02 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/OnboardWiz001Tests |

## Description

A user without an organization fills in the wizard, the organization is created and its dashboard opens with the user as Admin.

## Preconditions

- Not signed in.
- "Setup User" belongs to no organization.
- No organization named "Globex Corp" exists.

## Test Data

| Field | Value |
|-------|-------|
| Organization Name | "Globex Corp" |
| Legal Name | "Globex Corporation S.r.l." |
| VAT Number | "IT01234567890" |
| Billing Email | "billing@globex.test" |

## Steps

1. Open the home page.
2. Sign in as "Setup User".
3. Type "Globex Corp" in the "Organization Name" field.
4. Type "Globex Corporation S.r.l." in the "Legal Name" field.
5. Type "IT01234567890" in the "VAT Number" field.
6. Type "billing@globex.test" in the "Billing Email" field.
7. Click the "Submit" button.

## Expected Results

- The message "Organization created successfully!" appears.
- The Dashboard page opens and the header shows "Globex Corp".
- The menu shows "Settings".
