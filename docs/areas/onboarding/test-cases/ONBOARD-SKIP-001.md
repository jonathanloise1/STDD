# ONBOARD-SKIP-001 — Skip Onboarding Redirects to Dashboard

| Field | Value |
|---|---|
| **User Story** | US-ONBOARD-02 |
| **Type** | E2E |
| **Priority** | Medium |
| **Automated** | Planned |

## Description

Verify that a first-time user can skip the onboarding wizard and land on the dashboard.

## Preconditions

- User is authenticated (via impersonation) with `HasCompletedOnboarding = false`.

## Test Data

| Field | Value |
|-------|-------|
| User | New User (first login) |

## Steps

1. Log in as a new user (impersonation).
2. Verify the onboarding wizard is displayed.
3. Click "Skip" on Step 1 (Language).
4. Verify redirect to the dashboard.
5. Verify `HasCompletedOnboarding` is now `true` (wizard does not appear on next visit).

## Expected Results

- User lands on the dashboard.
- Language remains `de` (default).
- Wizard does not reappear.

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| POST | `/api/users/me/complete-onboarding` | 200 OK |

---

*Last updated: June 2025*
