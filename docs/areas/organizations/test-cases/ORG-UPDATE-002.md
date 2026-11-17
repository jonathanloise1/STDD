# ORG-UPDATE-002: An editor cannot open the Update Organization page

| Field | Value |
|-------|-------|
| User Story | US-ORG-02 |
| Type | Security |
| Priority | High |
| Automated by | e2e/OrgUpdate002Tests |

## Description

An editor who opens the Update Organization page by its address is taken to the Dashboard with a message, and no field is shown.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Update Organization page.

## Expected Results

- The Dashboard page opens with the message "You don't have access to this page."
- The page does not show the fields "Organization Name" or "Legal Name".
