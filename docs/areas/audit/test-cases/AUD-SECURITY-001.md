# AUD-SECURITY-001 — Only Admin can access audit data

| Field | Value |
|-------|-------|
| **User Story** | US-AUD-03, US-AUD-04 |
| **Type** | Security |
| **Priority** | High |
| **Automated** | Yes |

## Description

Verify that only Admin users can access audit data via the API and the frontend. Editor and Viewer roles are denied access.

## Preconditions

- [x] Users exist with Admin, Editor, and Viewer roles in the same organization
- [x] Audit records exist in the organization

## Test Data

| Role | Expected Access |
|------|----------------|
| Admin | Full access (200 OK) |
| Editor | Denied (403 Forbidden) |
| Viewer | Denied (403 Forbidden) |

## Steps

1. **Admin**: `GET /api/organizations/{orgId}/audit` → verify 200 OK with data.
2. **Admin**: `GET /api/organizations/{orgId}/audit/{auditLogId}` → verify 200 OK.
3. **Editor**: `GET /api/organizations/{orgId}/audit` → verify 403 Forbidden.
4. **Editor**: `GET /api/organizations/{orgId}/audit/{auditLogId}` → verify 403 Forbidden.
5. **Viewer**: `GET /api/organizations/{orgId}/audit` → verify 403 Forbidden.
6. **Viewer**: `GET /api/organizations/{orgId}/audit/{auditLogId}` → verify 403 Forbidden.
7. **Editor**: navigate to a Task detail page → verify "History" tab is not visible.
8. **Viewer**: navigate to organization settings → verify "Audit Trail" section is not visible.

## Expected Results

- [x] Admin gets 200 OK on all audit endpoints
- [x] Editor gets 403 Forbidden on all audit endpoints
- [x] Viewer gets 403 Forbidden on all audit endpoints
- [x] Frontend hides audit-related UI elements for non-Admin users
- [x] Entity detail "History" tab is hidden for non-Admin users
- [x] No audit data is leaked via error messages

## API Calls

| Method | Endpoint | Expected Status (Editor/Viewer) |
|--------|----------|--------------------------------|
| GET | `/api/organizations/{orgId}/audit` | 403 Forbidden |
| GET | `/api/organizations/{orgId}/audit/{auditLogId}` | 403 Forbidden |
