# Audit Trail - Test Suite

## Coverage Matrix

| Test Case | Title | User Story | Type | Priority |
|-----------|-------|------------|------|----------|
| [AUD-LIST-001](test-cases/AUD-LIST-001.md) | The Audit Log page lists the changes of the organization | US-AUD-01 | Happy path | High |
| [AUD-LIST-002](test-cases/AUD-LIST-002.md) | A new task appears at the top of the list | US-AUD-01 | Business rule | High |
| [AUD-LIST-003](test-cases/AUD-LIST-003.md) | An organization without changes shows an empty message | US-AUD-01 | UX | Medium |
| [AUD-LIST-004](test-cases/AUD-LIST-004.md) | Next and Previous move between pages of 25 changes | US-AUD-01 | UX | Medium |
| [AUD-LIST-005](test-cases/AUD-LIST-005.md) | A viewer cannot open the audit trail | US-AUD-01 | Security | High |
| [AUD-LIST-006](test-cases/AUD-LIST-006.md) | An editor cannot open the audit trail | US-AUD-01 | Security | High |
| [AUD-LIST-007](test-cases/AUD-LIST-007.md) | Loading the changes when the service does not answer shows a message | US-AUD-01 | Error handling | High |
| [AUD-FILTER-001](test-cases/AUD-FILTER-001.md) | Choosing a type shows only changes of that type | US-AUD-02 | Happy path | Medium |
| [AUD-FILTER-002](test-cases/AUD-FILTER-002.md) | A date range without changes shows an empty table | US-AUD-02 | Business rule | Medium |
| [AUD-FILTER-003](test-cases/AUD-FILTER-003.md) | Reset clears all filters | US-AUD-02 | UX | Low |
| [AUD-FILTER-004](test-cases/AUD-FILTER-004.md) | The Type field keeps every choice after filtering | US-AUD-02 | Business rule | Medium |
| [AUD-DETAIL-001](test-cases/AUD-DETAIL-001.md) | An edited task shows its old and new title | US-AUD-03 | Happy path | High |
| [AUD-DETAIL-002](test-cases/AUD-DETAIL-002.md) | A created task shows only new values | US-AUD-03 | Business rule | Medium |
| [AUD-DETAIL-003](test-cases/AUD-DETAIL-003.md) | Cancel closes the details dialog | US-AUD-03 | UX | Low |

## Seed Data

- "MyApp Dev Org" has the Admin "Dev User", an Editor and a Viewer.
- "MyApp Dev Org" has at least 30 recorded changes of types "Task" and "Team member", all made by "Dev User" within the last month. Changes made while loading the seed data are not recorded by the app, so the seed data writes this history itself.
- "Empty Org" has the Admin "Empty Admin" and no recorded changes.
