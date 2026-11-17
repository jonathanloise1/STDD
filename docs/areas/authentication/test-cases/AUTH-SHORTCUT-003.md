# AUTH-SHORTCUT-003: Typing in a field does not trigger shortcuts

| Field | Value |
|-------|-------|
| User Story | US-AUTH-09 |
| Type | Business rule |
| Priority | Low |
| Automated by | e2e/AuthShortcut003Tests |

## Description

Letters typed in a field are text: typing G and D in the Title field keeps the user on the Tasks page.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "gd" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "gd" in the "Title" field.

## Expected Results

- The Tasks page is still shown with the form open.
- The "Title" field shows "gd".
