# AUD-PANEL-001 — Audit trail panel timeline view

| Field | Value |
|-------|-------|
| **User Story** | US-AUD-04 |
| **Type** | Happy path |
| **Priority** | Medium |
| **Automated** | Yes |

## Description

Verify that the frontend Audit Trail panel displays a timeline of changes with filtering, expandable details, and entity-specific history integration.

## Preconditions

- [x] Admin user is authenticated
- [x] Multiple audit records exist across entity types and users
- [x] At least one Task has 3+ audit records (create, update, delete)

## Test Data

| Entry | Icon | Entity | Action | User | Timestamp |
|-------|------|--------|--------|------|----------|
| 1 | 🗑️ | Task "Review Documentation" | Delete | Admin | 2025-03-20 14:30 |
| 2 | ✏️ | Task "Fix Login Bug" | Update | Editor | 2025-03-20 10:15 |
| 3 | ➕ | Task "Review Documentation" | Create | Editor | 2025-03-15 09:00 |

## Steps

1. Navigate to the Audit Trail panel (from organization settings).
2. Verify timeline displays recent changes in reverse chronological order.
3. Verify each entry shows: action icon, entity type, entity name/ID, user, timestamp.
4. Expand entry 2 (Update) and verify old → new values are displayed.
5. Filter by entity type "Task" and verify only Task entries shown.
6. Filter by user "Editor" and verify only Editor's changes shown.
7. Filter by action type "Delete" and verify only delete entries shown.
8. Filter by date range and verify entries within range only.
9. Navigate to a Task detail page and click the "History" tab.
10. Verify only audit records for that specific Task are shown.

## Expected Results

- [x] Timeline view displays changes in reverse chronological order
- [x] Each entry shows icon, entity type, entity name/ID, user, and timestamp
- [x] Expandable detail shows old → new values for each changed property
- [x] Entity type filter dropdown works
- [x] User filter dropdown works
- [x] Date range filter works
- [x] Action type filter works
- [x] Entity-specific "History" tab on detail pages works
- [x] Pagination (infinite scroll or page numbers) works

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| GET | `/api/organizations/{orgId}/audit` | 200 OK |
| GET | `/api/organizations/{orgId}/audit?entityType=Policy&entityId={policyId}` | 200 OK |
