# ORG-TEAM-004: An editor cannot open the Team page

| Field | Value |
|-------|-------|
| User Story | US-ORG-04 |
| Type | Security |
| Priority | High |
| Automated by | e2e/OrgTeam004Tests |

## Description

An editor has no "Team" menu item, and opening the Team page by its address leads to the Dashboard with a message.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Team page.

## Expected Results

- The Dashboard page opens with the message "You don't have access to this page."
- The page does not show "Team Management".
- The menu has no "Team" link.
