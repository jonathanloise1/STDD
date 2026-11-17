# AUD-DETAIL-002: A created task shows only new values

| Field | Value |
|-------|-------|
| User Story | US-AUD-03 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/AudDetail002Tests |

## Description

A creation has no old values, so the dialog shows only the new ones, with the status as it reads on the Tasks page.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Details check" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Details check" in the "Title" field.
4. Click the "Create Task" button.
5. Click the "Audit Log" link in the menu.
6. Click the "Details" button on the first row of the table.

## Expected Results

- The "Details" dialog shows the action "Create".
- "New Values" shows "Title" with "Details check" and "Status" with "Todo".
- "Old Values" is not shown.
