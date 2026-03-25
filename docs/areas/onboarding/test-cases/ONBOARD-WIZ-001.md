# ONBOARD-WIZ-001 — Onboarding Wizard Sets Language and Redirects

| Field | Value |
|---|---|
| **User Story** | US-ONBOARD-01 |
| **Type** | E2E |
| **Priority** | High |
| **Automated** | Planned |

## Description

Verify that a first-time user sees the onboarding wizard, can select a language, and is redirected to the task creation page upon completion.

## Preconditions

- User is authenticated (via impersonation) with `HasCompletedOnboarding = false`.
- User has a default language of `de`.

## Test Data

| Field | Value |
|-------|-------|
| User | New User (first login) |
| Selected language | `fr` |

## Steps

1. Log in as a new user (impersonation).
2. Verify the onboarding wizard is displayed.
3. On Step 1 (Language), select French (`fr`).
4. Click "Next".
5. On Step 2 (Welcome), click "Create your first task".
6. Verify redirect to the task creation page.
7. Refresh the page — verify the wizard does NOT appear again.
8. Call `POST /api/auth/sync` — verify `preferredLanguage` is `fr`.

## Expected Results

- Wizard shows on first login.
- Language is saved as `fr`.
- User lands on the task creation page.
- Wizard does not reappear on subsequent visits.

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| PUT | `/api/users/me/language` | 200 OK |
| POST | `/api/users/me/complete-onboarding` | 200 OK |
| POST | `/api/auth/sync` | 200 OK |

---

*Last updated: June 2025*
