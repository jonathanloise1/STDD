# AUTH-LANG-001 — User Sets Preferred Language

| Field | Value |
|-------|-------|
| **User Story** | US-AUTH-08 |
| **Type** | E2E |
| **Priority** | High |
| **Automated** | Planned |

## Description

Verify that a user can set their preferred language and the platform reflects the choice across UI and subsequent sessions.

## Preconditions

- User A exists and is authenticated (via impersonation).
- User A belongs to an organization.
- Default language is `de` (German).

## Test Data

| Field | Value |
|-------|-------|
| User | Test User A |
| Initial language | `de` |
| Target language | `fr` |

## Steps

1. Navigate to the profile / settings page.
2. Locate the language preference selector.
3. Change language from `de` to `fr`.
4. Verify the UI updates to French immediately.
5. Refresh the page.
6. Verify the language is still `fr` after reload (persisted).
7. Call `POST /api/auth/sync` and verify the response includes `preferredLanguage: "fr"`.

## Expected Results

- Language selector shows DE, FR, IT options.
- After selecting FR, all UI labels switch to French.
- Language persists across page refreshes.
- Sync response includes the updated language preference.

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| PUT | `/api/users/me/language` | 200 OK |
| POST | `/api/auth/sync` | 200 OK (includes `preferredLanguage`) |

---

*Last updated: June 2025*
