# AUTH-NAV-002: The open page is highlighted in the menu

| Field | Value |
|-------|-------|
| User Story | US-AUTH-06 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/AuthNav002Tests |

## Description

Clicking a menu entry opens its page and highlights only that entry.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Tasks" link in the menu.

## Expected Results

- The Tasks page opens.
- The "Tasks" entry is highlighted.
- The "Dashboard" entry is not highlighted.
