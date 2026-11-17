# US-AUD-03: See what changed in an entry

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-AUD-02 |
| User input | No |

## Story

**As an** organization admin
**I want** to open a change and see the values before and after it
**So that** I can understand exactly what was modified

## Acceptance Criteria

1. Each row of the table has a "Details" button that opens a dialog titled "Details".
2. The dialog shows the "Date/Time", "Action", "Type" and "User" of the change; the time is the time on the user's device.
3. For an "Update", the dialog shows "Old Values" and "New Values" with only the fields that changed.
4. For a "Create", the dialog shows only "New Values" with the data entered.
5. For a "Delete", the dialog shows only "Old Values" with the data that was removed.
6. Fields and values are shown as on the screens where they are edited, for example "Title" and the status "In Progress".
7. The "Cancel" button closes the dialog and the list stays as it was.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |

## Business Rules

1. Old values exist only for updates and deletions; new values exist only for creations and updates.

## Error States

None.

## Non-Goals

- Restoring the old values.
- Comparing two entries side by side.

## Questions users ask

### en

1. Can I see what a task said before somebody edited it?
2. How do I find out which field was modified?
3. What does the old and new values box mean?
4. I opened an entry about a deleted item. What is shown for it?

### it

1. Posso vedere cosa c'era scritto prima che qualcuno modificasse un'attività?
2. Come capisco quale campo è stato cambiato?
3. Cosa significano i valori precedenti e i nuovi valori?
4. Cosa vedo se apro una voce relativa a qualcosa di cancellato?
