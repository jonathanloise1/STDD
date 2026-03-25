# AUD-QUERY-001 — Query audit log with filters

| Field | Value |
|-------|-------|
| **User Story** | US-AUD-03 |
| **Type** | Happy path |
| **Priority** | Medium |
| **Automated** | Yes |

## Description

Verify that the audit log API supports filtering by entity type, entity ID, user, action, date range, and pagination.

## Preconditions

- [x] Admin user is authenticated
- [x] At least 60 audit records exist across different entity types, actions, and users
- [x] Records span multiple dates

## Test Data

| Record | EntityType | Action | UserId | Timestamp |
|--------|-----------|--------|--------|-----------|
| 1-20 | Policy | Create | UserA | 2025-01-10 |
| 21-40 | Conversation | Update | UserB | 2025-02-15 |
| 41-60 | Organization | Delete | UserA | 2025-03-20 |

## Steps

1. `GET /api/organizations/{orgId}/audit` → verify returns first 50 records (default page size), newest first.
2. `GET .../audit?page=2&pageSize=50` → verify returns remaining 10 records.
3. `GET .../audit?entityType=Policy` → verify only Policy records returned.
4. `GET .../audit?entityType=Policy&entityId={policyId}` → verify records for that specific policy.
5. `GET .../audit?userId={UserA}` → verify only UserA's records.
6. `GET .../audit?action=Update` → verify only Update actions.
7. `GET .../audit?from=2025-02-01&to=2025-02-28` → verify only February records.
8. Combine filters: `?entityType=Conversation&action=Update&userId={UserB}` → verify intersection.

## Expected Results

- [x] Default pagination: page 1, 50 records, ordered by Timestamp descending
- [x] Page 2 returns remaining records
- [x] EntityType filter works correctly
- [x] EntityId filter narrows to a specific entity
- [x] UserId filter returns only that user's changes
- [x] Action filter returns only the specified action type
- [x] Date range filter includes records within the range
- [x] Combined filters return the intersection of all criteria
- [x] Each record includes: entity type, entity ID, action, user name, timestamp, changed properties

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| GET | `/api/organizations/{orgId}/audit` | 200 OK |
| GET | `/api/organizations/{orgId}/audit?entityType=Policy` | 200 OK |
| GET | `/api/organizations/{orgId}/audit?action=Update&userId={id}` | 200 OK |
| GET | `/api/organizations/{orgId}/audit?from=2025-02-01&to=2025-02-28` | 200 OK |
