# ORG-TEAM-001: The Team page lists the members with their roles

| Field | Value |
|-------|-------|
| User Story | US-ORG-04 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/OrgTeam001Tests |

## Description

An admin opens the Team page from the menu and sees each member with the role.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Click the "Team" link in the menu.

## Expected Results

- The Team page opens with the title "Team Management - MyApp Dev Org".
- The "Members" table has a row "Dev User", "dev@myapp.local", "Admin".
- The "Members" table has a row "Dev Member", "member@myapp.local", "Editor".
- The "Members" table has a row "Dev Viewer", "viewer@myapp.local", "Viewer".
