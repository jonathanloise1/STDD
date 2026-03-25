# AUD-QUERY-002 — View detail with old and new values

| Field | Value |
|-------|-------|
| **User Story** | US-AUD-03 |
| **Type** | Happy path |
| **Priority** | Medium |
| **Automated** | Yes |

## Description

Verify that the audit detail endpoint returns the full JSON of old and new values for a specific audit record.

## Preconditions

- [x] Admin user is authenticated
- [x] A Task was updated (Title changed from "Review Documentation" to "Review API Documentation")
- [x] The Update AuditLog record ID is known

## Test Data

| Field | Value |
|-------|-------|
| AuditLog ID | `{updateAuditId}` |
| EntityType | Task |
| Action | Update |
| OldValues | `{"Title":"Review Documentation"}` |
| NewValues | `{"Title":"Review API Documentation"}` |
| ChangedProperties | `["Title"]` |

## Steps

1. Call `GET /api/organizations/{orgId}/audit/{auditLogId}`.
2. Verify the response contains the full OldValues JSON.
3. Verify the response contains the full NewValues JSON.
4. Verify ChangedProperties lists `["Title"]`.
5. Verify UserDisplayName is populated (denormalized, no join needed).
6. Query a Create audit record and verify OldValues is null.
7. Query a Delete audit record and verify NewValues is null.

## Expected Results

- [x] Detail endpoint returns full JSON values
- [x] OldValues shows the previous state for Update and Delete
- [x] NewValues shows the new state for Create and Update
- [x] ChangedProperties is populated for Update actions
- [x] OldValues is null for Create actions
- [x] NewValues is null for Delete actions
- [x] UserDisplayName is available without additional joins

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| GET | `/api/organizations/{orgId}/audit/{auditLogId}` | 200 OK |
