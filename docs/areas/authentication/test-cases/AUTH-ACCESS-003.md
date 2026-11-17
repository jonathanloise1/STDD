# AUTH-ACCESS-003: A Viewer sees no management pages

| Field | Value |
|-------|-------|
| User Story | US-AUTH-07 |
| Type | Security |
| Priority | High |
| Automated by | e2e/AuthAccess003Tests |

## Description

A Viewer's menus offer no management page, and opening the Organization page by its address leads back to the Dashboard with a message.

## Preconditions

- Signed in as Viewer of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Dev Viewer" button.
3. Open the Organization page.
4. Click the "Tasks" link in the menu.

## Expected Results

- The menu shows only "Dashboard" and "Tasks".
- After step 2, the avatar menu shows "Logout" and no "Settings".
- After step 3, the Dashboard page opens with the message "You don't have access to this page."
- After step 4, the Tasks page opens.
