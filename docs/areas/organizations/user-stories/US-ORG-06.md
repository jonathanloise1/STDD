# US-ORG-06: Change the role of a team member

| Field | Value |
|-------|-------|
| Priority | Medium |
| Feature | F-ORG-02 |
| User input | No |

## Story

**As an** organization admin
**I want** to give a member a different role
**So that** each person has the right level of access

## Acceptance Criteria

1. On the Team page, the "Edit" button of a row opens the "Edit Team Member" dialog with the member's name, email and the roles "Admin", "Editor" and "Viewer".
2. The current role of the member is selected; the name and the email are shown but cannot be changed.
3. The "Save" button stays disabled until another role is chosen.
4. After saving, the dialog closes, the message "Member updated successfully" appears and the row shows the new role.
5. When an Admin chooses a lower role for themselves, the dialog warns "You are removing your own Admin privileges. This action cannot be undone by yourself."
6. Clicking "Cancel" closes the dialog and nothing changes.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |

## Business Rules

1. An Admin who lowers their own role must confirm: "Save" is replaced by "Go Back" and "Confirm Demotion".
2. An organization always keeps at least one Admin.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The change would leave the organization without an Admin | "An organization needs at least one Admin." | Give another member the Admin role first |
| Another admin removed the member while the dialog was open | "This member is no longer in the team." | Continue with the updated table, where the member is no longer shown |

## Non-Goals

- Changing the name or the email of a member.
- Changing several members at once.

## Questions users ask

### en

1. How do I let a viewer start editing?
2. Why is the save button greyed out?
3. What happens if I take admin rights away from myself?
4. Why can't I stop being an admin of my company?

### it

1. Come faccio a far modificare i dati a chi può solo guardare?
2. Perché il pulsante per salvare è grigio?
3. Cosa succede se tolgo a me stesso i diritti di amministratore?
4. Perché non riesco a smettere di essere amministratore della mia azienda?
