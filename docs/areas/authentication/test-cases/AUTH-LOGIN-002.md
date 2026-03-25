# AUTH-LOGIN-002: Unauthorized Access Without Authentication

| Field | Value |
|-------|-------|
| **User Story** | US-AUTH-01 |
| **Type** | E2E |
| **Priority** | High |
| **Automated** | Planned |

## Description

Verify that an unauthenticated user cannot access protected API endpoints.

## Preconditions

- Backend running.
- No impersonation headers set.

## Steps

1. Send `POST /api/auth/sync` without `Authorization` header.
2. Send `GET /api/organizations` without `Authorization` header.

## Expected Results

- Both requests return `401 Unauthorized`.
- No user data is leaked in the response body.

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| POST | `/api/auth/sync` | 401 |
| GET | `/api/organizations` | 401 |
