# AUTH-SHORTCUT-001: G then T opens the Tasks page

| Field | Value |
|-------|-------|
| User Story | US-AUTH-09 |
| Type | Happy path |
| Priority | Low |
| Automated by | e2e/AuthShortcut001Tests |

## Description

An Admin moves between pages with the keyboard: G then T opens the Tasks page and G then M the Team page.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Press "G" then "T".
3. Press "G" then "M".

## Expected Results

- After step 2, the Tasks page opens.
- After step 3, the Team page opens.
