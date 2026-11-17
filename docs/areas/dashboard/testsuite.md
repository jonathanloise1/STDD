# Dashboard - Test Suite

## Coverage Matrix

| Test Case | Title | User Story | Type | Priority |
|-----------|-------|------------|------|----------|
| [DASH-EMPTY-002](test-cases/DASH-EMPTY-002.md) | An organization without tasks shows zero on every card | US-DASH-01 | UX | Low |
| [DASH-KPI-001](test-cases/DASH-KPI-001.md) | The dashboard shows the task counts by status | US-DASH-01 | Happy path | High |
| [DASH-KPI-002](test-cases/DASH-KPI-002.md) | Cancelled tasks are counted only in the total | US-DASH-01 | Business rule | Medium |
| [DASH-KPI-003](test-cases/DASH-KPI-003.md) | A visitor is asked to sign in before seeing the dashboard | US-DASH-01 | Security | High |
| [DASH-KPI-004](test-cases/DASH-KPI-004.md) | Loading the counts when the service does not answer shows a message | US-DASH-01 | Error handling | Medium |
| [DASH-EMPTY-001](test-cases/DASH-EMPTY-001.md) | An organization without tasks shows a hint | US-DASH-02 | UX | Medium |
| [DASH-RECENT-001](test-cases/DASH-RECENT-001.md) | Recent tasks show title, status and due date | US-DASH-02 | Happy path | High |
| [DASH-RECENT-002](test-cases/DASH-RECENT-002.md) | Only the five newest tasks are listed | US-DASH-02 | Business rule | Medium |
| [DASH-RECENT-003](test-cases/DASH-RECENT-003.md) | Loading recent tasks when the service does not answer shows a message | US-DASH-02 | Error handling | Medium |
| [DASH-ACTIONS-001](test-cases/DASH-ACTIONS-001.md) | The View All button opens the task list | US-DASH-03 | Happy path | Medium |
| [DASH-ACTIONS-002](test-cases/DASH-ACTIONS-002.md) | Clicking a recent task row opens the task list | US-DASH-03 | Happy path | Low |
| [DASH-GREET-001](test-cases/DASH-GREET-001.md) | The dashboard greets the user by name | US-DASH-04 | Happy path | Low |
| [DASH-GREET-002](test-cases/DASH-GREET-002.md) | Italian users read the greeting in Italian | US-DASH-04 | UX | Low |

## Seed Data

- "MyApp Dev Org" has the Admin "Dev User", whose language is English, and the demo tasks described in the Tasks area.
- "Stats Org" has the members "Stats Admin" (Admin, never signs in) and "Stats Viewer" (Viewer), and twelve tasks "Stats task 01" to "Stats task 12", created in that order on different days. Tasks 01 to 05 are "Todo", 06 to 08 "In Progress", 09 to 11 "Done" and 12 "Cancelled". Every task has a due date except "Stats task 12". Being read-only, no test changes them.
- "Empty Org" has one member, "Empty Admin" (Admin), and no tasks.
