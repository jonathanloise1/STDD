# AUD-FILTER-003: Reset clears all filters

| Field | Value |
|-------|-------|
| User Story | US-AUD-02 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/AudFilter003Tests |

## Description

The Reset button returns the filters to their starting values and shows every change again.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- "MyApp Dev Org" has changes on record of type "Task".

## Test Data

| Field | Value |
|-------|-------|
| From | "2099-12-31" |

## Steps

1. Click the "Audit Log" link in the menu.
2. Type "2099-12-31" in the "From" field.
3. Click the "Reset" button.

## Expected Results

- The "Type" field shows "All types".
- The "From" and "To" fields are empty.
- The table lists the changes of "MyApp Dev Org" again.
