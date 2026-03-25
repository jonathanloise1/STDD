# AUD-INTERCEPT-002 — Excluded entities are not audited

| Field | Value |
|-------|-------|
| **User Story** | US-AUD-01 |
| **Type** | Business rule |
| **Priority** | High |
| **Automated** | Yes |

## Description

Verify that entities excluded from auditing (AuditLog, Message) do not generate AuditLog records, even when they are created, updated, or deleted.

## Preconditions

- [x] User is authenticated with Admin role
- [x] A conversation with messages exists
- [x] AuditLog count is noted before the test

## Test Data

| Entity | Expected Audited |
|--------|-----------------|
| Policy | Yes |
| Conversation | Yes |
| Organization | Yes |
| AuditLog | No (self-referential exclusion) |
| Message | No (high-volume AI-generated data) |

## Steps

1. Note the current AuditLog count.
2. Send a message in a conversation (creates Message records).
3. Query the AuditLog.
4. Verify NO audit records exist for Message.
5. Verify that the Conversation creation IS audited.
6. Create a Policy and verify it IS audited.

## Expected Results

- [x] AuditLog entity is excluded (prevents infinite recursion)
- [x] Message is excluded (high-volume AI-generated data)
- [x] Non-excluded entities (Policy, Conversation, Organization) are still audited normally
- [x] No performance degradation from high-volume message operations

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| POST | `/api/conversations/{conversationId}/messages` | 201 Created |
| GET | `/api/organizations/{orgId}/audit?entityType=Message` | 200 OK (empty) |
| GET | `/api/organizations/{orgId}/audit?entityType=Conversation` | 200 OK (non-empty) |
