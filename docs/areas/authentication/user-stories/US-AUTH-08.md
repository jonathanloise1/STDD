# US-AUTH-08: Change the app language

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-AUTH-04 |
| User input | No |

## Story

**As a** signed-in user
**I want** to switch the app between English and Italian
**So that** I can work in the language I prefer

## Acceptance Criteria

1. The app is available in English and Italian; a user who has not chosen a language sees English.
2. The "Change language" button in the header, shown on every screen size, displays the current language ("EN" or "IT") and opens a menu with "English" and "Italiano".
3. Choosing a language changes the menu, labels and messages at once, without reloading the page.
4. After a choice, a message confirms it, for example "Language changed to English".
5. The chosen language is kept the next time the user signs in, on any device.

## Access Control

| Role | Allowed |
|------|---------|
| Signed-in user | Yes |

## Business Rules

1. Only English and Italian are offered; English is the default.
2. The language belongs to the user, not to the organization.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the choice was saved; the app keeps the language only until the user signs out | "The service did not answer. Try again in a few minutes." | Choose the language again later so that it is kept at the next sign-in |

## Non-Goals

- Translating the content users typed, such as task titles.
- Offering other languages.

## Questions users ask

### en

1. How can I see the app in Italian?
2. I switched language yesterday and today it's back to English. Why?
3. Does changing my language change it for my colleagues too?
4. Where is the little globe with EN on it on my phone?

### it

1. Come faccio a mettere l'app in italiano?
2. Ho cambiato lingua ieri e oggi è tornata in inglese, perché?
3. Se cambio lingua la cambio anche ai miei colleghi?
4. Dal telefono dove trovo il pulsante con EN per scegliere la lingua?
