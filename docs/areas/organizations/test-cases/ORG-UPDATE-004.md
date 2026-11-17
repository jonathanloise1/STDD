# ORG-UPDATE-004: An invalid billing email shows a field message

| Field | Value |
|-------|-------|
| User Story | US-ORG-02 |
| Type | Validation |
| Priority | Medium |
| Automated by | e2e/OrgUpdate004Tests |

## Description

A Billing Email that is not a valid address cannot be saved.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Billing Email | "billing-at-example" |

## Steps

1. Click the "Settings" link in the menu.
2. Click the "Edit" button.
3. Clear the "Billing Email" field.
4. Type "billing-at-example" in the "Billing Email" field.
5. Click the "Save Changes" button.

## Expected Results

- The "Billing Email" field shows "Invalid email format".
- The Update Organization page stays open.
