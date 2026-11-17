# ORG-UPDATE-001: Edit the city of the organization

| Field | Value |
|-------|-------|
| User Story | US-ORG-02 |
| Type | Happy path |
| Priority | Critical |
| Automated by | e2e/OrgUpdate001Tests |

## Description

An admin changes the city of the organization and sees the new value on the Organization page.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| City | "Torino" |
| City (restored) | "Milano" |

## Steps

1. Click the "Settings" link in the menu.
2. Click the "Edit" button.
3. Clear the "City" field.
4. Type "Torino" in the "City" field.
5. Click the "Save Changes" button.
6. Click the "Edit" button.
7. Clear the "City" field.
8. Type "Milano" in the "City" field.
9. Click the "Save Changes" button.

## Expected Results

- After the first save, the message "Organization has been updated successfully" appears and the Organization page opens.
- After the first save, "City" shows "Torino (MI)".
- After the second save, "City" shows "Milano (MI)" again.
