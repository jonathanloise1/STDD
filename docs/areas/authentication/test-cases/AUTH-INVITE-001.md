# AUTH-INVITE-001: Pending Membership Auto-Activated on Login

| Field | Value |
|-------|-------|
| **User Story** | US-AUTH-05 |
| **Type** | E2E |
| **Priority** | Critical |
| **Automated** | Planned |

## Description

Verify that when a user with a pending organization membership logs in (syncs), the membership is automatically activated and the user joins the organization.

## Preconditions

- Test User C exists in DB with email `testc@MyApp.test`.
- Organization "Invited Org" exists.
- An `OrganizationUser` record exists with `Email = testc@MyApp.test`, `Status = Pending`, `UserId = null`.

## Steps

1. Configure impersonation for Test User C.
2. Call `POST /api/auth/sync`.
3. Verify the response `organizations` array now includes "Invited Org".
4. Query DB: `OrganizationUser.Status = Active`, `UserId = Test User C's Id`.

## Expected Results

- Sync response includes the newly joined organization.
- Organization user record updated: `Status = Active`, `UserId` set.

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| POST | `/api/auth/sync` | 200 |
