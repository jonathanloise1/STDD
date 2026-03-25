# DASH-EMPTY-001 — Empty State Shows Create CTA

| Field | Value |
|---|---|
| **User Story** | US-DASH-01 |
| **Type** | E2E |
| **Priority** | Medium |
| **Automated** | Planned |

## Description

Verify that when no tasks exist, the dashboard shows an empty state with a CTA to create the first task.

## Preconditions

- User is authenticated (via impersonation).
- Organization has zero tasks.

## Steps

1. Log in as a user in an organization with no tasks.
2. Navigate to the dashboard.
3. Verify the empty state message is displayed.
4. Verify a "Create your first task" CTA is visible.

## Expected Results

- Empty state message and CTA are displayed.
- Clicking the CTA navigates to the task creation form.

---

*Last updated: March 2026*