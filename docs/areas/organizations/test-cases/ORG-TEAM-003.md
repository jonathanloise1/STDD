# ORG-TEAM-003: A search without results shows a message

| Field | Value |
|-------|-------|
| User Story | US-ORG-04 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/OrgTeam003Tests |

## Description

Typing text that matches nobody shows a message in the table.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Search team members... | "zzzz" |

## Steps

1. Click the "Team" link in the menu.
2. Type "zzzz" in the "Search team members..." field.

## Expected Results

- The table shows "No team members match your search".
