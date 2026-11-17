# AUTH-SHORTCUT-004: The question mark opens the shortcuts help

| Field | Value |
|-------|-------|
| User Story | US-AUTH-09 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/AuthShortcut004Tests |

## Description

Pressing the question mark opens a dialog that explains the shortcuts; an Admin sees all five pages.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Press "?".

## Expected Results

- The "Keyboard Shortcuts" dialog opens with the text "Press G then a letter to navigate to a section."
- The dialog lists "Dashboard", "Tasks", "Audit Log", "Team" and "Settings".
