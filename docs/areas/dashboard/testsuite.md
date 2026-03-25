# Dashboard — Test Suite

> **User Stories**: [userstories.md](./userstories.md)

## Coverage Matrix

| Test Case | US-DASH-01 | US-DASH-02 | US-DASH-03 | Type | Priority | Automated |
|-----------|:----------:|:----------:|:----------:|------|----------|-----------|
| DASH-SUMMARY-001 | ✓ | | | E2E | High | ✅ Done |
| DASH-EMPTY-001 | ✓ | | | E2E | Medium | Planned |
| DASH-RECENT-001 | | ✓ | | E2E | High | Planned |
| DASH-ACTIONS-001 | | | ✓ | E2E | Medium | ✅ Done |

**Total: 4 test cases (2 automated, 2 planned)**

## Test Case Summary

| ID | Title | Type | US |
|---|---|---|---|
| DASH-SUMMARY-001 | [Dashboard shows task counts](./test-cases/DASH-SUMMARY-001.md) | E2E | US-DASH-01 |
| DASH-EMPTY-001 | [Empty state shows create CTA](./test-cases/DASH-EMPTY-001.md) | E2E | US-DASH-01 |
| DASH-RECENT-001 | [Recent tasks are displayed](./test-cases/DASH-RECENT-001.md) | E2E | US-DASH-02 |
| DASH-ACTIONS-001 | [Quick action links navigate correctly](./test-cases/DASH-ACTIONS-001.md) | E2E | US-DASH-03 |

## Testing Strategy

All dashboard tests use the impersonation pattern. Task data is seeded before tests run.

### Test Data

| Scenario | Tasks |
|----------|-------|
| With data | 5 tasks across 3 statuses (2 Todo, 2 InProgress, 1 Done) |
| Empty | No tasks in organization |

---

*Last updated: March 2026*