# ORG-VIEW-002: A viewer cannot open the Organization page

| Field | Value |
|-------|-------|
| User Story | US-ORG-01 |
| Type | Security |
| Priority | High |
| Automated by | e2e/OrgView002Tests |

## Description

A viewer has no "Settings" menu item, and opening the Organization page by its address leads to the Dashboard with a message.

## Preconditions

- Signed in as Viewer of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Organization page.

## Expected Results

- The Dashboard page opens with the message "You don't have access to this page."
- The page does not show "Billing Information".
- The menu has no "Settings" link.
