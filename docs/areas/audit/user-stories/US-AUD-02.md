# US-AUD-02: Filter the audit trail

| Field | Value |
|-------|-------|
| Priority | Medium |
| Feature | F-AUD-01 |
| User input | No |

## Story

**As an** organization admin
**I want** to narrow the list of changes by type and by date
**So that** I can find a specific change quickly

## Acceptance Criteria

1. The Audit Log page has a "Type" field that starts as "All types" and always offers "Task", "Team member", "Organization" and "User".
2. Choosing a type shows only the changes of that type.
3. The "From" and "To" fields limit the list to changes made from the start of the first date to the end of the second date, by the time on the user's device.
4. Type and dates can be used together; only changes that match all of them are listed.
5. Changing any filter shows the first page of the result again.
6. The "Reset" button clears the three filters and shows all changes again.
7. When nothing matches the filters, the table shows "No audit logs found."

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |

## Business Rules

1. Filters look through the whole history of the organization, not only the changes visible on the current page.
2. The choices of the "Type" field do not depend on the changes listed.

## Error States

None.

## Non-Goals

- Filtering by user.
- Searching by text.
- Saving a filter for later.

## Questions users ask

### en

1. How do I see only what happened to tasks?
2. Can I look at the changes of one particular week?
3. I set a date and now the table is empty. Why?
4. How do I get the full list back after searching?

### it

1. Come faccio a vedere solo le modifiche alle attività?
2. Posso guardare cosa è successo in una certa settimana?
3. Ho scelto una data e la tabella è vuota, perché?
4. Come torno a vedere tutto dopo aver cercato qualcosa?
