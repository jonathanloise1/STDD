# US-ORG-05: Invite a team member

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-ORG-02 |
| User input | Yes |

## Story

**As an** organization admin
**I want** to invite a colleague by email and give them a role
**So that** they can work in my organization

## Acceptance Criteria

1. On the Team page, the "Invite" button opens the "Invite Team Member" dialog.
2. The dialog has the fields Full Name and Email and a choice of role between "Admin", "Editor" and "Viewer", each with a short description.
3. "Viewer" is selected when the dialog opens.
4. Clicking "Send Invitation" with an empty field shows "Full name is required" or "Email address is required" under it.
5. An email that is not a valid address shows "Please enter a valid email address".
6. After sending, the dialog closes, the message "Invitation sent successfully" appears and the person is listed under "Pending Invitations".
7. Clicking "Cancel" closes the dialog and nobody is invited.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |

## Business Rules

1. An invited person is listed under "Pending Invitations", not under "Members", until they sign in.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The email belongs to someone who is already a member | "User is already a member of this organization." | Use another email, or change the role of that member |
| The email already has a pending invitation | "A pending invitation for this email already exists." | Wait for the person to sign in; no second invitation is needed |

## Non-Goals

- Inviting several people at once.
- Resending or cancelling a pending invitation.

## Questions users ask

### en

1. How do I let a new colleague into our account?
2. Which role should I pick for someone who only needs to look?
3. Why does it say the person is already there?
4. The person I added is waiting, when do they become a member?

### it

1. Come faccio entrare un nuovo collega nel nostro account?
2. Quale ruolo scelgo per chi deve solo guardare?
3. Perché mi dice che la persona c'è già?
4. La persona che ho aggiunto è in attesa, quando diventa membro?
