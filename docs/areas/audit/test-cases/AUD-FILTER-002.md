# AUD-FILTER-002: A date range without changes shows an empty table

| Field | Value |
|-------|-------|
| User Story | US-AUD-02 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/AudFilter002Tests |

## Description

Dates limit the list, and filters are combined with the type.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- "MyApp Dev Org" has changes on record of type "Task".

## Test Data

| Field | Value |
|-------|-------|
| From | "2099-12-31" |

## Steps

1. Click the "Audit Log" link in the menu.
2. Select "Task" in the "Type" field.
3. Type "2099-12-31" in the "From" field.

## Expected Results

- The table shows "No audit logs found."
