# DASH-ACTIONS-001 — Quick Action Links Navigate Correctly

| Field | Value |
|---|---|
| **User Story** | US-DASH-03 |
| **Type** | E2E |
| **Priority** | Medium |
| **Automated** | Yes |

## Description

Verify that quick action links on the dashboard navigate to the correct pages.

## Preconditions

- User is authenticated (via impersonation).

## Steps

1. Log in as a standard user.
2. Navigate to the dashboard.
3. Click "View All Tasks" link.
4. Verify navigation to the task list page.

## Expected Results

- "View All Tasks" link navigates to `/tasks`.

---

*Last updated: March 2026*