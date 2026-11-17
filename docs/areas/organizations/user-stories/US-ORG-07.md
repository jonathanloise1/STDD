# US-ORG-07: Remove a team member

| Field | Value |
|-------|-------|
| Priority | Medium |
| Feature | F-ORG-02 |
| User input | No |

## Story

**As an** organization admin
**I want** to take a person out of my organization
**So that** people who left no longer have access

## Acceptance Criteria

1. On the Team page, the rows of Editors and Viewers have a "Remove" button; the rows of Admins do not.
2. The "Remove" button opens the "Confirm Removal" dialog with the text "Are you sure you want to remove" followed by the member's name and "from the team? This action cannot be undone."
3. Clicking "Remove" in the dialog closes it, shows the message "Member removed successfully" and the member is no longer in the table.
4. Clicking "Cancel" closes the dialog and the member stays in the table.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |

## Business Rules

1. Admins cannot be removed from the Team page; their role must be lowered first.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| Another admin removed the member before the removal was confirmed | "This member is no longer in the team." | Continue with the updated table, where the member is no longer shown |

## Non-Goals

- Removing an Admin; lower the role first.
- Cancelling a pending invitation.

## Questions users ask

### en

1. A colleague left the company, how do I stop their access?
2. Why is there no bin icon next to the owner of the account?
3. I took someone out by mistake, what now?

### it

1. Un collega ha lasciato l'azienda, come blocco il suo accesso?
2. Perché non c'è il cestino accanto al proprietario dell'account?
3. Ho tolto una persona per errore, cosa faccio?
