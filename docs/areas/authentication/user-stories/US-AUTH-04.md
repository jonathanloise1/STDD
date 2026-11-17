# US-AUTH-04: Start using the app on first sign-in

| Field | Value |
|-------|-------|
| Priority | Critical |
| Feature | F-AUTH-02 |
| User input | No |

## Story

**As a** new user
**I want** to be able to use the app the first time I sign in
**So that** I do not have to register separately

## Acceptance Criteria

1. The first time an email address signs in, no registration form appears.
2. A person who belongs to no organization sees the "Set Up Your Organization" page right after signing in.
3. Signing in again with the same email address opens the same account with the same organizations.
4. The app of a new account is in English until the user chooses another language.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | Yes |

## Business Rules

1. The account is created by signing in; nothing else is needed.
2. Signing in again never creates a second account and never repeats the user's organizations.
3. The "Set Up Your Organization" page appears only to people who belong to no organization; it is the only page they can use.

## Error States

None.

## Non-Goals

- Creating the organization itself: that happens on the setup page (Onboarding area).
- Sending a welcome email.
- Merging two accounts that use different email addresses.

## Questions users ask

### en

1. Do I need to sign up first, or can I just log in?
2. Why am I asked to set up an organization the first time?
3. If I sign in a second time, will I get a duplicate account?
4. Why is everything in English on my first visit?

### it

1. Devo registrarmi prima o posso accedere subito?
2. Perché alla prima visita mi chiede di creare un'organizzazione?
3. Se accedo di nuovo mi ritrovo con un secondo profilo?
4. Perché la prima volta vedo tutto in inglese?
