# ORG-TEAM-002: The search ignores upper and lower case

| Field | Value |
|-------|-------|
| User Story | US-ORG-04 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/OrgTeam002Tests |

## Description

Typing a name in lower case finds the member whose name is written with capitals.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Search team members... | "dev viewer" |

## Steps

1. Click the "Team" link in the menu.
2. Type "dev viewer" in the "Search team members..." field.

## Expected Results

- The "Members" table shows the row "Dev Viewer".
- The "Members" table does not show the rows "Dev User" and "Dev Member".
