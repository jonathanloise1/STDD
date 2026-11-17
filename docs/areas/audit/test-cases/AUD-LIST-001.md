# AUD-LIST-001: The Audit Log page lists the changes of the organization

| Field | Value |
|-------|-------|
| User Story | US-AUD-01 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/AudList001Tests |

## Description

An admin opens the Audit Log page from the menu and reads the table of changes with its total count.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- "MyApp Dev Org" has more than 25 changes on record, of types "Task" and "Team member".

## Test Data

None.

## Steps

1. Click the "Audit Log" link in the menu.

## Expected Results

- The Audit Log page opens.
- The table shows the columns "Date/Time", "Action", "Type", "User" and "Changes".
- The table shows 25 rows.
- A count above the table shows the total number of changes.
