# AUD-FILTER-001: Choosing a type shows only changes of that type

| Field | Value |
|-------|-------|
| User Story | US-AUD-02 |
| Type | Happy path |
| Priority | Medium |
| Automated by | e2e/AudFilter001Tests |

## Description

Filtering by type hides the changes of other types.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- "MyApp Dev Org" has changes on record of types "Task" and "Team member".

## Test Data

None.

## Steps

1. Click the "Audit Log" link in the menu.
2. Select "Task" in the "Type" field.

## Expected Results

- Every row of the table shows the type "Task".
- The count above the table is lower than before.
