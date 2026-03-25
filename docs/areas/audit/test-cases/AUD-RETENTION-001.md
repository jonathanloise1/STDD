# AUD-RETENTION-001 — Retention cleanup removes old records

| Field | Value |
|-------|-------|
| **User Story** | US-AUD-05 |
| **Type** | Happy path |
| **Priority** | Low |
| **Automated** | Yes |

## Description

Verify that the retention cleanup background job removes (or archives) audit records older than the configured retention period, while preserving recent records.

## Preconditions

- [x] Retention period is configured to 365 days (default)
- [x] Audit records exist from various dates including some older than 365 days
- [x] Background cleanup job can be triggered manually for testing

## Test Data

| Record | Timestamp | Age (days) | Expected Outcome |
|--------|-----------|------------|-----------------|
| Record A | 2025-01-01 | 445 | Deleted / archived |
| Record B | 2025-06-15 | 280 | Preserved |
| Record C | 2026-01-10 | 70 | Preserved |

## Steps

1. Insert test audit records with timestamps: 445 days ago, 280 days ago, and 70 days ago.
2. Trigger the retention cleanup job (or wait for scheduled execution).
3. Query all audit records.
4. Verify Record A (445 days old) has been deleted or marked as archived.
5. Verify Record B (280 days old) is still present.
6. Verify Record C (70 days old) is still present.
7. Change retention period to 200 days in configuration.
8. Trigger cleanup again.
9. Verify Record B (280 days old) is now also removed.

## Expected Results

- [x] Records older than the retention period are deleted or archived
- [x] Records within the retention period are preserved
- [x] Retention period is configurable (default 365 days)
- [x] Cleanup job handles empty result sets gracefully
- [x] Audit records for deleted entities are subject to the same retention policy
- [x] Cleanup job completes without errors

## API Calls

| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| N/A | Background job (HostedService) | N/A |
| GET | `/api/organizations/{orgId}/audit` | 200 OK |
