# AUTH-NAV-001: A member with an organization lands on the Dashboard page

| Field | Value |
|-------|-------|
| User Story | US-AUTH-06 |
| Type | Happy path |
| Priority | Critical |
| Automated by | e2e/AuthNav001Tests |

## Description

After signing in, an Admin of an organization lands on the Dashboard page and finds the full menu.

## Preconditions

- Not signed in.
- "Dev User" is the Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the home page.
2. Sign in as Admin.

## Expected Results

- The Dashboard page opens.
- The menu lists "Dashboard", "Tasks", "Team", "Audit Log" and "Settings".
- The header shows "MyApp Dev Org".
