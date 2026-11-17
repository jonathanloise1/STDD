# AUTH-SHORTCUT-002: An Editor has only the shortcuts of Dashboard and Tasks

| Field | Value |
|-------|-------|
| User Story | US-AUTH-09 |
| Type | Business rule |
| Priority | Low |
| Automated by | e2e/AuthShortcut002Tests |

## Description

For an Editor, the shortcut to the Team page does nothing and the help dialog lists only the pages the role allows.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Press "G" then "M".
3. Press "?".

## Expected Results

- After step 2, the Dashboard page is still shown and no message appears.
- After step 3, the "Keyboard Shortcuts" dialog lists "Dashboard" and "Tasks" and does not list "Team", "Audit Log" or "Settings".
