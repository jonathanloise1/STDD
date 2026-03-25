# AUD-INTERCEPT-003 — Correlation ID groups related changes

| Field | Value |
|-------|-------|
| **User Story** | US-AUD-01 |
| **Type** | Business rule |
| **Priority** | Medium |
| **Automated** | Yes |

## Description

Verify that when a single user action produces multiple entity changes (e.g., cascade operations, bulk import), all resulting AuditLog records share the same CorrelationId for grouping.

## Preconditions

- [x] User is authenticated with Admin role
- [x] The user has policies and conversations

## Test Data

| Trigger Action | Expected Changes |
|----------------|------------------|
| Delete organization (cascade) | Organization + N Tasks |
| Bulk task creation (3 tasks) | 3 Task creates |

## Steps

1. Delete an organization that has 5 tasks.
2. Query the AuditLog for the most recent records.
3. Verify all audit records from this operation share the same CorrelationId.
4. Verify the CorrelationId is a non-null, non-empty string.
5. Perform a bulk upload of 3 policies.
6. Verify all 3 Create audit records share a single CorrelationId.
7. Verify the CorrelationId from step 6 is different from step 3.

## Expected Results

- [x] All changes from a cascade delete share one CorrelationId
- [x] All changes from a bulk upload share one CorrelationId
- [x] Different operations produce different CorrelationIds
- [x] CorrelationId is non-null for multi-entity operations
- [x] Single-entity operations may have null or unique CorrelationId

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| DELETE | `/api/conversations/{conversationId}` | 200 OK |
| GET | `/api/organizations/{orgId}/audit?correlationId={correlationId}` | 200 OK |
