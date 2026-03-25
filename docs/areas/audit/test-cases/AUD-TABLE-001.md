# AUD-TABLE-001 — AuditLog table and tenant isolation

| Field | Value |
|-------|-------|
| **User Story** | US-AUD-02 |
| **Type** | Happy path |
| **Priority** | High |
| **Automated** | Yes |

## Description

Verify that the AuditLog table is correctly created with the required indexes and that tenant isolation via OrganizationId prevents cross-tenant data leakage.

## Preconditions

- [x] Two organizations exist: Org A and Org B
- [x] Admin users exist in both organizations
- [x] Both organizations have entities that have been changed (audit records exist)

## Test Data

| Organization | Action | Entity |
|-------------|--------|--------|
| Org A | Create Task "Review Documentation" | Task |
| Org B | Create Task "Fix Login Bug" | Task |

## Steps

1. As Admin of Org A, create a Task "Review Documentation".
2. As Admin of Org B, create a Task "Fix Login Bug".
3. As Admin of Org A, query `GET /api/organizations/{orgAId}/audit`.
4. Verify only Org A's audit records are returned.
5. Verify Task "Fix Login Bug" does NOT appear in Org A's audit log.
6. As Admin of Org B, query `GET /api/organizations/{orgBId}/audit`.
7. Verify only Org B's audit records are returned.
8. Verify the global query filter enforces OrganizationId isolation.

## Expected Results

- [x] Audit records include OrganizationId for tenant isolation
- [x] Global query filter prevents cross-tenant access
- [x] Admin of Org A cannot see Org B's audit records
- [x] Indexes exist: IX_AuditLog_EntityType_EntityId, IX_AuditLog_OrganizationId_Timestamp, IX_AuditLog_UserId, IX_AuditLog_CorrelationId
- [x] No cascade delete — audit records survive entity deletion

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| GET | `/api/organizations/{orgAId}/audit` | 200 OK |
| GET | `/api/organizations/{orgBId}/audit` | 200 OK |
