# Audit Trail - Functional Requirements

## Overview

The Audit Trail captures every data change (create, update, delete) across all tracked entities via an EF Core `SaveChanges` interceptor. This provides a complete, append-only history of who changed what, when, and what the old/new values were. The audit log is essential for accountability and for regulatory compliance.

The interceptor should be implemented **early** — retrofitting audit is impossible (you cannot recover history that was never recorded).

## Entity Relationships

```
AuditLog                   ← append-only, never modified or deleted
  ├── EntityType           ← "Task", "Organization", "User", etc.
  ├── EntityId             ← the PK of the changed entity
  ├── Action               ← Create, Update, Delete
  ├── UserId               ← who made the change
  └── Changes              ← JSON: old values vs. new values
```

## Data Model

### AuditLog

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | GUID | PK | |
| OrganizationId | GUID | FK → Organization | Tenant isolation |
| EntityType | nvarchar(100) | Required | e.g., "Task", "Organization", "User" |
| EntityId | nvarchar(100) | Required | String representation of the PK (supports GUID or composite) |
| Action | int | Required | 0=Create, 1=Update, 2=Delete |
| UserId | GUID | FK → User | Who triggered the change |
| UserDisplayName | nvarchar(200) | | Denormalized for display without join |
| OldValues | nvarchar(max) | JSON, nullable | Null for Create actions |
| NewValues | nvarchar(max) | JSON, nullable | Null for Delete actions |
| ChangedProperties | nvarchar(max) | JSON, nullable | List of property names that changed (for Update) |
| Timestamp | datetime2 | Required | UTC |
| CorrelationId | nvarchar(100) | Optional | Groups related changes (e.g., bulk import, cascade delete) |

### Key Behaviors

- **Append-only**: AuditLog rows are never updated or deleted.
- **EF Core interceptor**: hooks into `SaveChanges` / `SaveChangesAsync` to capture all changes automatically, with no manual logging calls needed.
- **JSON values**: `OldValues` and `NewValues` store a JSON object with property names and their values. Only changed properties are included in `OldValues`/`NewValues` for Update actions.
- **Excluded entities**: the AuditLog itself is excluded (to prevent infinite recursion). Message entities may be excluded for performance (high-volume chat operations should not generate audit rows per message).
- **Correlation ID**: when a single user action produces multiple entity changes (e.g., deleting a task cascades to related metadata), all audit rows share a `CorrelationId` for grouping.

## User Stories

| ID | Story | Priority |
|----|-------|----------|
| US-AUD-01 | EF Core SaveChanges interceptor | High |
| US-AUD-02 | AuditLog entity and table | High |
| US-AUD-03 | API: get audit log for entity | Medium |
| US-AUD-04 | Frontend: audit trail panel | Medium |
| US-AUD-05 | Audit log retention and cleanup | Low |

---

## US-AUD-01: EF Core SaveChanges Interceptor

**As the** system
**I want to** automatically capture all entity changes on SaveChanges
**So that** every data modification is recorded without requiring manual logging

### Acceptance Criteria

- An EF Core `SaveChangesInterceptor` (or override of `SaveChangesAsync` in `DbContext`) is implemented.
- On every `SaveChanges` call, the interceptor:
  1. Iterates `ChangeTracker.Entries()` for entities in `Added`, `Modified`, or `Deleted` state.
  2. For each changed entity, creates an `AuditLog` record with:
     - `EntityType` = entity class name.
     - `EntityId` = primary key value (converted to string).
     - `Action` = Create / Update / Delete based on state.
     - `OldValues` = JSON of original values (for Update and Delete).
     - `NewValues` = JSON of current values (for Create and Update).
     - `ChangedProperties` = list of modified property names (for Update only).
     - `UserId` = extracted from the current `HttpContext.User` (or a system user for background jobs).
     - `Timestamp` = `DateTime.UtcNow`.
  3. Audit records are added to the same `DbContext` and saved in the same transaction as the original changes.
- **Excluded entities**: `AuditLog`, `Message` (high-volume chat data that doesn't need row-level auditing).
- **Performance**: the interceptor adds minimal overhead. JSON serialization is done only for changed properties, not full entity graphs.

### Business Rules

1. Audit records are saved in the **same transaction** — if the original save fails, no audit record is created.
2. Navigation properties are **not** serialized — only scalar/primitive properties.
3. Sensitive fields (if any) can be excluded via an `[AuditIgnore]` attribute.
4. The `UserId` is resolved from `IHttpContextAccessor`. For background/system operations, a well-known system user ID is used.

---

## US-AUD-02: AuditLog Entity and Table

**As a** developer
**I want to** persist audit records in a dedicated table
**So that** the audit trail is stored reliably and queryable

### Acceptance Criteria

- The `AuditLog` entity is defined per the data model above.
- EF Core migration creates the `AuditLogs` table.
- Indexes:
  - `IX_AuditLog_EntityType_EntityId` (for entity-specific queries).
  - `IX_AuditLog_OrganizationId_Timestamp` (for org-wide timeline).
  - `IX_AuditLog_UserId` (for user activity queries).
  - `IX_AuditLog_CorrelationId` (for grouped operations).
- The table uses `OrganizationId` for tenant isolation (consistent with all other tables).
- Global query filter applies `OrganizationId` just like other entities.

### Business Rules

1. No cascade delete — audit records survive even if the referenced entity is deleted.
2. The table can grow large. Archival/cleanup is handled by US-AUD-05.

---

## US-AUD-03: API - Get Audit Log for Entity

**As an** Admin
**I want to** view the change history of a specific entity
**So that** I can see who changed what and when

### Acceptance Criteria

- `GET /api/organizations/{orgId}/audit?entityType={type}&entityId={id}` returns audit records for a specific entity.
- `GET /api/organizations/{orgId}/audit?entityType={type}` returns recent audit records for an entity type.
- `GET /api/organizations/{orgId}/audit` returns recent audit records across all entities.
- Supports:
  - Pagination: `?page=1&pageSize=50` (default 50, max 200).
  - Date range: `?from={date}&to={date}`.
  - User filter: `?userId={userId}`.
  - Action filter: `?action=Update`.
- Each audit record includes: entity type, entity ID, action, user name, timestamp, changed properties list.
- Full `OldValues` / `NewValues` JSON is available via: `GET /api/organizations/{orgId}/audit/{auditLogId}`.
- Results ordered by `Timestamp` descending (newest first).

### Access Control

**Admin only**. Audit data is sensitive — only Admins can view change history.

### Error Responses

| Status | Condition |
|--------|-----------|
| 403 | User is not Admin |

---

## US-AUD-04: Frontend - Audit Trail Panel

**As an** Admin
**I want to** see the audit trail in the UI
**So that** I can review changes without using the API directly

### Acceptance Criteria

- An "Audit Trail" section is accessible from the organization settings or from a dedicated navigation item.
- The panel shows a **timeline view** of recent changes:
  - Each entry: icon (create/update/delete), entity type, entity name/ID, user, timestamp, summary of changes.
  - Expandable detail: shows old → new values for each changed property.
- Supports filtering:
  - By entity type (dropdown: Task, Organization, User, etc.).
  - By user (dropdown of organization members).
  - By date range.
  - By action type (Create / Update / Delete).
- Supports entity-specific audit: on any entity detail page (e.g., Task detail), a "History" tab shows changes to that specific entity.
- Paginated (infinite scroll or page numbers).

### Access Control

**Admin only**.

---

## US-AUD-05: Audit Log Retention and Cleanup

**As an** Admin
**I want** old audit records to be archived or cleaned up
**So that** the audit table doesn't grow unbounded

### Acceptance Criteria

- A configurable retention period per organization (default: 365 days).
- Audit records older than the retention period are:
  - Option A: Soft-marked as "Archived" and excluded from default queries.
  - Option B: Physically deleted by a scheduled job.
- MVP: configurable via `appsettings.json`. Future: configurable per organization in the UI.
- A scheduled background job (HostedService or Azure Function timer) runs daily/weekly to enforce retention.

### Business Rules

1. MVP: 365-day retention, cleanup via background job.
2. Audit records for deleted entities are kept (they prove that something was deleted and by whom).
3. No archival export for MVP (future: export to blob storage before deletion).

---

## Business Rules Summary

| # | Rule | Applies to |
|---|------|-----------|
| BR-01 | Audit records are append-only, never modified | All |
| BR-02 | Interceptor captures Create, Update, Delete on all tracked entities | Interceptor |
| BR-03 | AuditLog, Message are excluded from auditing | Interceptor |
| BR-04 | Audit records are saved in the same transaction as the entity change | Interceptor |
| BR-05 | Only scalar properties are serialized, not navigation properties | Interceptor |
| BR-06 | Correlation ID groups related changes from a single operation | Interceptor |
| BR-07 | Retention period: 365 days default | Cleanup |
| BR-08 | Only Admins can view audit data | API, Frontend |

## Access Control Summary

| Action | Admin | Editor | Viewer |
|--------|:-----:|:------:|:------:|
| View audit log | ✅ | ❌ | ❌ |
| View entity history | ✅ | ❌ | ❌ |
| Configure retention | ✅ | ❌ | ❌ |

---

*Last updated: March 2026*
