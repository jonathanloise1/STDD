# AUTH-LOGOUT-001: Logout Clears Session and Blocks Protected Access

| Field | Value |
|-------|-------|
| **User Story** | US-AUTH-06 |
| **Type** | E2E |
| **Priority** | High |
| **Automated** | Planned |

## Description

Verify that after logout, the user's session is completely cleared and protected routes are no longer accessible.

## Preconditions

- Test User A is authenticated (impersonation set up).
- User is on a protected page.

## Steps

1. Set up impersonation for Test User A.
2. Navigate to `/company/dashboard` — verify it loads.
3. Trigger logout (click logout button or clear impersonation tokens).
4. Clear the `E2E_MOCK_USER` from localStorage.
5. Remove the `X-MyApp-Impersonate-UserId` header.
6. Navigate to `/company/dashboard`.
7. Verify the user is redirected to the login page.
8. Call `POST /api/auth/sync` without auth headers.
9. Verify the response is `401 Unauthorized`.

## Expected Results

- After logout, protected pages redirect to login.
- API calls without auth return `401`.
- No residual session data in localStorage.

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| POST | `/api/auth/sync` | 401 |
