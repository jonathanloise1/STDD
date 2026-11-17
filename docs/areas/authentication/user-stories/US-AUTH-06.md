# US-AUTH-06: Move between the sections of the app

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-AUTH-03 |
| User input | No |

## Story

**As a** signed-in user
**I want** to move between the sections of the app from one menu
**So that** I can reach my work quickly

## Acceptance Criteria

1. After signing in, a member of an organization lands on the Dashboard page.
2. The menu lists "Dashboard" and "Tasks"; Admins also see "Team", "Audit Log" and, at the bottom, "Settings".
3. Clicking a menu entry opens its page and the entry of the open page is highlighted.
4. The header of every page shows the name of the current organization.
5. A user who belongs to several organizations chooses the current one by clicking its name in the header; every page then shows the data of the chosen organization.
6. An address that matches no page shows "Page not found" and a "Homepage" link that leads back to the Dashboard page.

## Access Control

| Role | Allowed |
|------|---------|
| Signed-in user | Yes |

## Business Rules

1. Only the entry of the page that is open is highlighted.

## Error States

None.

## Non-Goals

- Searching the app from the header.
- Changing the order of the menu entries.

## Questions users ask

### en

1. Where do I find my tasks once I'm logged in?
2. Which screen should I see first after logging in?
3. I typed a wrong address and got an error page. How do I get back?
4. I work for two companies, how do I switch between them?

### it

1. Dove trovo le mie attività una volta entrato?
2. Quale schermata dovrei vedere subito dopo l'accesso?
3. Ho scritto male un indirizzo e mi esce una pagina di errore, come torno indietro?
4. Lavoro per due aziende, come passo dall'una all'altra?
