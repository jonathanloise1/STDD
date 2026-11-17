# US-AUD-01: Open the audit trail

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-AUD-01 |
| User input | No |

## Story

**As an** organization admin
**I want** to open the audit trail and read the list of changes made in my organization
**So that** I can see who changed what and when

## Acceptance Criteria

1. Admins see an "Audit Log" link in the menu; clicking it opens the Audit Log page.
2. The page lists the changes in a table with the columns "Date/Time", "Action", "Type", "User" and "Changes"; the type reads "Task", "Team member", "Organization" or "User".
3. The newest change is at the top of the table, and a count above the table shows how many changes are listed in total.
4. The table shows 25 changes per page; "Previous" and "Next" move between pages and the page position is shown as "1 / 3".
5. Creating, editing or deleting a task, a team member or the organization adds a change with the action "Create", "Update" or "Delete" and the name of the person who made it.
6. For an "Update", the "Changes" column lists the changed fields by the labels used on the screens where they are edited, for example "Title"; for "Create" and "Delete" it shows "-".
7. When the organization has no changes yet, the table shows "No audit logs found."
8. Editors and Viewers do not see the "Audit Log" link; opening the page by its address takes them to the Dashboard page with the message "You don't have access to this page."

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Admin | Yes |
| Editor | No |
| Viewer | No |

## Business Rules

1. Only the changes of the organization currently selected are listed.
2. Changes cannot be edited or removed by any user.
3. Changes older than one year are not listed.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the changes were loaded | "The service did not answer. Try again in a few minutes." | Reload the page later |

## Non-Goals

- Exporting or printing the list.
- Undoing a change.
- Recording who opened or viewed data.

## Questions users ask

### en

1. Where can I check who changed things in our company account?
2. I cannot find the history link in my menu. Where did it go?
3. How far back does the list of modifications go?
4. The log page says nothing was found. Is something broken?

### it

1. Dove vedo chi ha modificato i dati della mia azienda?
2. Nel menu non trovo il registro delle modifiche, dov'è finito?
3. Per quanto tempo restano visibili le modifiche passate?
4. Sono un editor: posso consultare lo storico delle attività?
5. La pagina dello storico è vuota, è normale?
