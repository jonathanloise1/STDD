# US-AUTH-02: Get asked to sign in before using the app

| Field | Value |
|-------|-------|
| Priority | Critical |
| Feature | F-AUTH-01 |
| User input | No |

## Story

**As a** visitor who is not signed in
**I want** to be asked to sign in when I open the app
**So that** my organization's information stays private

## Acceptance Criteria

1. A visitor who opens any page of the app, the home address and the Dashboard page included, is taken to the sign-in page.
2. A visitor sees no menu, no organization name and no data before signing in.
3. After signing in, the page the visitor asked for opens; a visitor who opened the home address sees the Dashboard page.
4. A page the user's role does not allow is not opened after signing in; the Dashboard page opens with the message "You don't have access to this page."

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | Yes |

## Business Rules

1. Every page of the app needs a signed-in user; the sign-in page is the only page a visitor can reach.

## Error States

None.

## Non-Goals

- Showing a public home page or a preview of the app.
- Telling the visitor why the sign-in page opened.

## Questions users ask

### en

1. Why does the app keep sending me to a login screen?
2. I pasted a link to the task list and I can't see it. What is going on?
3. Can I look around the app before creating an account?
4. After logging in, do I come back to the page I was trying to open?

### it

1. Perché quando apro il sito mi chiede subito di accedere?
2. Ho aperto il link dell'elenco attività ma non vedo niente, come mai?
3. Posso dare un'occhiata all'app senza registrarmi?
4. Dopo l'accesso torno alla pagina che volevo aprire?
