# Audit Trail - Test Suite

> **User Stories**: [userstories.md](./userstories.md)

## Coverage Matrix

| Test Case | US-AUD-01 | US-AUD-02 | US-AUD-03 | US-AUD-04 | US-AUD-05 |
|-----------|:---------:|:---------:|:---------:|:---------:|:---------:|
| AUD-INTERCEPT-001 | ✓ | | | | |
| AUD-INTERCEPT-002 | ✓ | | | | |
| AUD-INTERCEPT-003 | ✓ | | | | |
| AUD-TABLE-001 | | ✓ | | | |
| AUD-QUERY-001 | | | ✓ | | |
| AUD-QUERY-002 | | | ✓ | | |
| AUD-PANEL-001 | | | | ✓ | |
| AUD-RETENTION-001 | | | | | ✓ |
| AUD-SECURITY-001 | | | ✓ | ✓ | |

## Test Case Summary

| ID | Title | Type | Priority |
|----|-------|------|----------|
| AUD-INTERCEPT-001 | [Interceptor captures Create, Update, Delete](./test-cases/AUD-INTERCEPT-001.md) | Happy path | High |
| AUD-INTERCEPT-002 | [Excluded entities are not audited](./test-cases/AUD-INTERCEPT-002.md) | Business rule | High |
| AUD-INTERCEPT-003 | [Correlation ID groups related changes](./test-cases/AUD-INTERCEPT-003.md) | Business rule | Medium |
| AUD-TABLE-001 | [AuditLog table and tenant isolation](./test-cases/AUD-TABLE-001.md) | Happy path | High |
| AUD-QUERY-001 | [Query audit log with filters](./test-cases/AUD-QUERY-001.md) | Happy path | Medium |
| AUD-QUERY-002 | [View detail with old and new values](./test-cases/AUD-QUERY-002.md) | Happy path | Medium |
| AUD-PANEL-001 | [Audit trail panel timeline view](./test-cases/AUD-PANEL-001.md) | Happy path | Medium |
| AUD-RETENTION-001 | [Retention cleanup removes old records](./test-cases/AUD-RETENTION-001.md) | Happy path | Low |
| AUD-SECURITY-001 | [Only Admin can access audit data](./test-cases/AUD-SECURITY-001.md) | Security | High |

## Test Data

**Audit scenario:**
- Organization with Admin, Editor, Viewer users
- A Task "Review Documentation" created, then updated (due date changed), then deleted
- Generates 3 AuditLog records (Create → Update → Delete) for verification

---

*Last updated: June 2025*
