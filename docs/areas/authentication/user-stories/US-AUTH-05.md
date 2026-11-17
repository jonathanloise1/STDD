# US-AUTH-05: Join an organization you were invited to

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-AUTH-02 |
| User input | No |

## Story

**As an** invited person
**I want** to join the organization that invited me by signing in
**So that** I can start working with my team without extra steps

## Acceptance Criteria

1. A person invited to an organization signs in with the email address the invitation was sent to.
2. After signing in, the Dashboard page of the organization opens and the header shows its name.
3. There is no step to accept the invitation, and the "Set Up Your Organization" page is not shown.
4. The invited person has the role given in the invitation: an Editor sees the "New Task" button on the Tasks page but no "Settings" menu item.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | Yes |

## Business Rules

1. An invitation is accepted by the first sign-in with the invited email address, whether the person is new or already has an account.
2. The role in the invitation decides what the person can do in that organization.

## Error States

None.

## Non-Goals

- Sending the invitation or choosing the role (done by an Admin of the organization).
- Declining an invitation.

## Questions users ask

### en

1. My manager invited me to the team, what do I do now?
2. I logged in but I can't see my company. Why?
3. Do I need to click a link to accept the invite?

### it

1. Il mio responsabile mi ha invitato nel team, cosa devo fare adesso?
2. Ho fatto l'accesso ma non vedo la mia azienda, perché?
3. Devo cliccare su qualcosa per accettare l'invito?
