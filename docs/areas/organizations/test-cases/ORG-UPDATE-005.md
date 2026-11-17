# ORG-UPDATE-005: A ZIP code that is not five digits shows a field message

| Field | Value |
|-------|-------|
| User Story | US-ORG-02 |
| Type | Validation |
| Priority | Medium |
| Automated by | e2e/OrgUpdate005Tests |

## Description

A ZIP / Postal Code that is not exactly five digits cannot be saved.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| ZIP / Postal Code | "201" |

## Steps

1. Click the "Settings" link in the menu.
2. Click the "Edit" button.
3. Clear the "ZIP / Postal Code" field.
4. Type "201" in the "ZIP / Postal Code" field.
5. Click the "Save Changes" button.

## Expected Results

- The "ZIP / Postal Code" field shows "ZIP code must be 5 digits".
- The Update Organization page stays open.
