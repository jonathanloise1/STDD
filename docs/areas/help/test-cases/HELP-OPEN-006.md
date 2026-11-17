# HELP-OPEN-006: Closing the panel returns to the unchanged screen

| Field | Value |
|-------|-------|
| User Story | US-HELP-03 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/HelpOpen006Tests |

## Description

After the panel is closed the user is back on the same screen with what they had typed.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Draft kept after help" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Draft kept after help" in the "Title" field.
4. Click the "Help" button.
5. Press the "Escape" key.

## Expected Results

- The panel closes.
- The "New Task" form is still open and the "Title" field shows "Draft kept after help".
