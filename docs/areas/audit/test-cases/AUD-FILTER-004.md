# AUD-FILTER-004: The Type field keeps every choice after filtering

| Field | Value |
|-------|-------|
| User Story | US-AUD-02 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/AudFilter004Tests |

## Description

The choices of the Type field do not depend on the changes listed, so the admin can switch from one type to another without resetting.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- "MyApp Dev Org" has changes on record of types "Task" and "Team member".

## Test Data

None.

## Steps

1. Click the "Audit Log" link in the menu.
2. Select "Task" in the "Type" field.
3. Select "Team member" in the "Type" field.

## Expected Results

- After step 2, the "Type" field still offers "All types", "Task", "Team member", "Organization" and "User".
- After step 3, every row of the table shows the type "Team member".
