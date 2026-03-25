# AUTH-SYNC-001: Sync Returns User ID, Email, and Organizations

| Field | Value |
|-------|-------|
| **User Story** | US-AUTH-04 |
| **Type** | E2E |
| **Priority** | Critical |
| **Automated** | Planned |

## Description

Verify that `POST /api/auth/sync` returns the correct user data and organization memberships.

## Preconditions

- Test User A exists in DB with one organization (Admin role, Active status).

## Steps

1. Configure impersonation for Test User A.
2. Call `POST /api/auth/sync` with body `{}`.
3. Verify the response structure:
   - `id` is a valid GUID.
   - `email` matches Test User A's email.
   - `organizations` is an array with at least one entry.
4. For the first organization entry, verify:
   - `organizationId` is a valid GUID.
   - `organizationName` is a non-empty string.
   - `role` is `"Admin"`.

## Expected Results

```json
{
  "id": "{guid}",
  "email": "testa@MyApp.test",
  "organizations": [
    {
      "organizationId": "{guid}",
      "organizationName": "Test Organization",
      "role": "Admin"
    }
  ]
}
```

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| POST | `/api/auth/sync` | 200 |
