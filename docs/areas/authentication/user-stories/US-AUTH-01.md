# US-AUTH-01: Sign in with your email address

| Field | Value |
|-------|-------|
| Priority | Critical |
| Feature | F-AUTH-01 |
| User input | No |

## Story

**As a** person with an email address
**I want** to sign in with my email and a code sent to me
**So that** I can use the app without remembering a password

## Acceptance Criteria

1. The sign-in page asks only for an email address; there is no password field.
2. After the email address is entered, a code is sent to that address and the page asks for it.
3. Entering the correct code opens the app; a member of an organization sees the Dashboard page.
4. Once signed in, the avatar button at the top right of every page shows the user's initials.
5. Reloading the page keeps the user signed in without asking for a new code.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | Yes |

## Business Rules

1. Email and code is the only way to sign in: there are no passwords and no sign-in with another service.
2. The user stays signed in while using the app; if the session can no longer be kept, the user is asked to sign in again.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the account was loaded after signing in | "The service did not answer. Try again in a few minutes." | Reload the page later |

## Non-Goals

- Choosing or changing a password.
- Signing in with a social account.
- The texts of the sign-in page, how long a code stays valid and the message for a wrong code: the sign-in service provides them.

## Questions users ask

### en

1. How do I get into the app if I never set a password?
2. Where do I find the code I need to log in?
3. Will I have to type a new code every time I refresh the browser?
4. Can I use my Google account to get in?

### it

1. Non ricordo la password, come faccio ad entrare?
2. Dove trovo il codice che mi chiede l'app per accedere?
3. Devo rimettere il codice ogni volta che ricarico la pagina?
4. Posso accedere con il mio account Google?
