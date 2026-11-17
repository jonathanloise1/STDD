# AUTH-ACCESS-004: A non-admin opening the Audit Log returns to the Dashboard

| Field | Value |
|-------|-------|
| User Story | US-AUTH-07 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/AuthAccess004Tests |

## Description

The role in the current organization decides: an Editor who opens the Audit Log page by its address is taken back to the Dashboard page and told why.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Audit Log page.

## Expected Results

- The Dashboard page opens with the message "You don't have access to this page."
- The Audit Log page is not shown.
