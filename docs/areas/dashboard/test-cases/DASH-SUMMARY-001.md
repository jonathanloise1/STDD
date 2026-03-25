# DASH-SUMMARY-001 — Dashboard Shows Task Counts

| Field | Value |
|---|---|
| **User Story** | US-DASH-01 |
| **Type** | E2E |
| **Priority** | High |
| **Automated** | Yes |

## Description

Verify that the dashboard displays KPI cards with correct task counts by status.

## Preconditions

- User is authenticated (via impersonation).
- Organization has tasks in various statuses.

## Test Data

| Status | Count |
|--------|-------|
| Todo | 2 |
| InProgress | 2 |
| Done | 1 |

## Steps

1. Log in as a standard user (impersonation).
2. Navigate to the dashboard.
3. Verify KPI cards are displayed: Total (5), Todo (2), In Progress (2), Done (1).

## Expected Results

- KPI cards show correct counts.
- Cards are visible without scrolling on desktop.

---

*Last updated: March 2026*