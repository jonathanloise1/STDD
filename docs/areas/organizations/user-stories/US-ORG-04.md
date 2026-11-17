# US-ORG-04: See the team members

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-ORG-02 |
| User input | No |

## Story

**As an** organization admin
**I want** to see who belongs to my organization and with which role
**So that** I know who can do what

## Acceptance Criteria

1. For Admins, the "Team" menu item opens the Team page, titled "Team Management" followed by the organization name.
2. The "Members" table shows Name, Email and Role of each member, with the role as a label "Admin", "Editor" or "Viewer".
3. Typing in the "Search team members..." field narrows the table to the members whose name or email contains the text.
4. When nothing matches, the table shows "No team members match your search".
5. People who were invited and have not signed in yet are listed under "Pending Invitations" with name, email and the date sent.
6. The back arrow of the page opens the Organization page.
7. Editors and Viewers do not see the "Team" menu item; opening the Team page by its address takes them to the Dashboard page with the message "You don't have access to this page."

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | No |
| Viewer | No |

## Business Rules

1. The search ignores upper and lower case.
2. The "Members" table lists only people who have joined; invited people appear only under "Pending Invitations".

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the team was loaded | "The service did not answer. Try again in a few minutes." | Reload the page later |

## Non-Goals

- Sorting the table or showing it in pages.
- Resending or cancelling a pending invitation.

## Questions users ask

### en

1. Where can I see everyone who has access to our account?
2. How do I find one colleague among many?
3. I invited someone but they are not in the list, where are they?
4. Why can't my colleagues open the page with the people of our company?

### it

1. Dove vedo tutte le persone che hanno accesso al nostro account?
2. Come trovo un collega in mezzo a tanti?
3. Ho invitato una persona ma non è nell'elenco, dov'è?
4. Perché i miei colleghi non riescono ad aprire la pagina con le persone dell'azienda?
