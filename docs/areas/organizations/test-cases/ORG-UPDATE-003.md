# ORG-UPDATE-003: Saving with empty required fields shows field messages

| Field | Value |
|-------|-------|
| User Story | US-ORG-02 |
| Type | Validation |
| Priority | High |
| Automated by | e2e/OrgUpdate003Tests |

## Description

Clearing required fields and saving keeps the page open and shows a message under each cleared field.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Click the "Settings" link in the menu.
2. Click the "Edit" button.
3. Clear the "Organization Name" field.
4. Clear the "Legal Name" field.
5. Click the "Save Changes" button.

## Expected Results

- The "Organization Name" field shows "Organization name is required".
- The "Legal Name" field shows "Legal name is required".
- The message "Please fix validation errors before submitting" appears.
- The Update Organization page stays open.
