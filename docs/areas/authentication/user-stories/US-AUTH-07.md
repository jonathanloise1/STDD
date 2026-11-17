# US-AUTH-07: Open only the pages your role allows

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-AUTH-03 |
| User input | No |

## Story

**As an** organization Admin
**I want** the management pages to be open only to Admins
**So that** Editors and Viewers cannot change how the organization is run

## Acceptance Criteria

1. Admins see the menu entries "Team", "Audit Log" and "Settings" and can open the Team, Audit Log, Organization and Update Organization pages.
2. Editors and Viewers see only "Dashboard" and "Tasks" in the menu, and their avatar menu offers only "Logout".
3. An Editor or a Viewer who opens one of the Admin pages by its address is taken to the Dashboard page with the message "You don't have access to this page."
4. The Dashboard and Tasks pages open for Admins, Editors and Viewers.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | No |
| Viewer | No |

## Business Rules

1. The role that counts is the one the user has in the organization currently selected in the header.

## Error States

None.

## Non-Goals

- Changing a member's role (done on the Team page).
- Pages for roles other than Admin, Editor and Viewer.

## Questions users ask

### en

1. Why don't I have the settings entry that my manager has?
2. Who is allowed to see the audit log?
3. I opened a link to the team page and landed on the home page, did I do something wrong?

### it

1. Perché nel mio menu manca la voce delle impostazioni che ha il mio responsabile?
2. Chi può vedere il registro delle attività?
3. Ho aperto un link alla pagina del team e sono finito nella pagina iniziale, ho sbagliato qualcosa?
