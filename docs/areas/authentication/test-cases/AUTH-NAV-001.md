# AUTH-NAV-001: User With Organizations Redirected to Dashboard

| Field | Value |
|-------|-------|
| **User Story** | US-AUTH-07 |
| **Type** | E2E |
| **Priority** | Critical |
| **Automated** | Planned |

## Description

Verify that after login, a user who belongs to at least one organization is redirected to `/company/dashboard`.

## Preconditions

- Test User A exists with at least one active organization membership.

## Steps

1. Configure impersonation for Test User A.
2. Navigate to the application root (`/`).
3. Wait for sync and navigation to complete.
4. Verify the URL is `/company/dashboard`.

## Expected Results

- User lands on `/company/dashboard`.
- Dashboard content is visible.
- No redirect loops.
