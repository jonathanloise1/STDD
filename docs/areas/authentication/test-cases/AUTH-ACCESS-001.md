# AUTH-ACCESS-001: An Admin opens the management pages from the menu

| Field | Value |
|-------|-------|
| User Story | US-AUTH-07 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/AuthAccess001Tests |

## Description

An Admin finds the management pages in the menu and can open each of them.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Settings" link in the menu.
3. Click the "Team" link in the menu.
4. Click the "Audit Log" link in the menu.

## Expected Results

- The menu shows "Dashboard", "Tasks", "Team", "Audit Log" and "Settings".
- The Organization page opens after step 2.
- The Team page opens after step 3.
- The Audit Log page opens after step 4.
