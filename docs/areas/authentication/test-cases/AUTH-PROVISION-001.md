# AUTH-PROVISION-001: First Login Creates a New User Record

| Field | Value |
|-------|-------|
| **User Story** | US-AUTH-02, US-AUTH-04 |
| **Type** | E2E |
| **Priority** | Critical |
| **Automated** | Planned |

## Description

Verify that when a user logs in for the first time, a new `User` record is created in the database with the correct data from the JWT claims.

## Preconditions

- Backend running with impersonation enabled.
- No user with the test AadId exists in the database.

## Test Data

| Field | Value |
|-------|-------|
| AadId | `{new unique GUID}` |
| Email | `new@MyApp.test` |
| FirstName | `New` |
| LastName | `User` |

## Steps

1. Configure impersonation with a new AadId that doesn't exist in the DB.
2. Call `POST /api/auth/sync`.
3. Verify the response contains a new `id` (internal GUID).
4. Verify `email` in the response matches the test email.
5. Query the database to confirm a `User` record was created.
6. Verify `User.AadId` matches the test AadId.
7. Verify `User.FirstName = "New"`, `User.LastName = "User"`.
8. Verify `User.CreatedAt` and `User.UpdatedAt` are set to approximately now (UTC).

## Expected Results

- `POST /api/auth/sync` returns `200 OK`.
- Response includes a new user `id` (GUID) and the email.
- `organizations` array is empty (new user has no memberships).
- Database contains a new `User` row with correct values.

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| POST | `/api/auth/sync` | 200 |
