# AUTH-LOGIN-001: Successful Login via Email + OTP (Impersonation)

| Field | Value |
|-------|-------|
| **User Story** | US-AUTH-01 |
| **Type** | E2E |
| **Priority** | Critical |
| **Automated** | Planned |

## Description

Verify that a user can log in via the impersonation pattern and that the frontend displays the authenticated state.

## Preconditions

- Backend running with `MyApp:Impersonation:Enabled = true`.
- Test User A exists in DB with at least one organization.

## Test Data

| Field | Value |
|-------|-------|
| UserId | `{Test User A GUID}` |
| Email | `testa@MyApp.test` |

## Steps

1. Navigate to the application root (`/`).
2. Set `E2E_MOCK_USER` in localStorage with Test User A's data.
3. Set `X-MyApp-Impersonate-UserId` header for API calls.
4. Navigate to a protected page (e.g., `/company/dashboard`).
5. Verify the page loads without redirect to login.
6. Verify the user's name or email is displayed in the UI (e.g., header/avatar area).

## Expected Results

- Protected page renders successfully.
- User identity is visible in the UI.
- No authentication errors in the browser console.
- `POST /api/auth/sync` returns `200 OK` with user data and organizations.

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| POST | `/api/auth/sync` | 200 |
