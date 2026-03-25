# DASH-RECENT-001 — Recent Tasks Are Displayed

| Field | Value |
|---|---|
| **User Story** | US-DASH-02 |
| **Type** | E2E |
| **Priority** | High |
| **Automated** | Planned |

## Description

Verify that the dashboard shows the most recent tasks with title, status badge, and due date.

## Preconditions

- User is authenticated (via impersonation).
- Organization has at least 3 tasks.

## Steps

1. Log in as a standard user.
2. Navigate to the dashboard.
3. Verify the "Recent Tasks" section is displayed.
4. Verify tasks show title, status badge, and due date.
5. Verify a "View All" link is present.

## Expected Results

- Recent tasks are displayed with correct data.
- Status badges are color-coded (green=Done, yellow=InProgress, red=Todo).
- "View All" link navigates to `/tasks`.

---

*Last updated: March 2026*